/*
Bimbo is a 2D platform game engine.
http://www.adammil.net
Copyright (C) 2004 Adam Milazzo

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Drawing;
using System.Collections;
using GameLib.Collections;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics;
using GameLib.Video;

namespace Bimbo
{

public class World
{ public string Name { get { return name; } }

  public void Load(string path)
  { path = path.Replace('\\', '/');
    if(path[path.Length-1] != '/') path += '/';

    Load(path, new List(System.IO.File.Open(path+"definition", System.IO.FileMode.Open, System.IO.FileAccess.Read)));
  }

  public void Load(string path, List list)
  { path = path.Replace('\\', '/');
    if(path[path.Length-1] != '/') path += '/';

    Unload();
    foreach(List child in list)
      if(child.Name=="polygon")
      { PolyType type;
        switch(child["type"].GetString(0))
        { case "solid": type=PolyType.Solid; break;
          case "water": type=PolyType.Water; break;
          default: throw new ArgumentException("Invalid polygon type: "+child["type"].GetString(0));
        }
        GameLib.Mathematics.TwoD.Polygon cpoly = new GameLib.Mathematics.TwoD.Polygon();
        foreach(List pt in child["points"]) cpoly.AddPoint(pt.ToPoint());
        foreach(GameLib.Mathematics.TwoD.Polygon p in cpoly.SplitIntoConvexPolygons())
        { Polygon poly = new Polygon(type, p);
          GameLib.Mathematics.TwoD.Rectangle bounds = poly.Poly.GetBounds();
          Rectangle brect = // TODO: maybe replace next 4 lines with foreach, using iterator??
            WorldToPart(new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height));
          for(int y=brect.Y; y<brect.Bottom; y++)
            for(int x=brect.X; x<brect.Right; x++)
              MakePartition(x, y).Polys.Add(poly);
        }
      }
      else if(child.Name=="layer") // FIXME: assumes layers are in order
      { List sub = child["tiles"];
        if(sub!=null)
          foreach(List tile in sub)
          { if(tile["zoom"].GetInt(0) != 1) continue;
            Point pos = tile["pos"].ToPoint();
            Partition part = MakePartitionW(pos);
            pos = TileOffset(pos);
            List color = tile["color"];
            part.Tiles[pos.X, pos.Y] = color==null ? new Tile(tile.GetString(0)) : new Tile(color.ToColor());
          }
      }
      else if(child.Name=="level-options")
      { List opt = child["bgColor"];
        if(opt!=null) bgColor = opt.ToColor();
        opt = child["name"];
        if(opt!=null) name = opt.GetString(0);
      }
    
    fsfile = new FSFile(path+"images.fsf");
    basePath = path;
  }

  public void Render(Camera camera, Size screenSize)
  { Point topLeft = new Point(camera.Point.X-screenSize.Width/2, camera.Point.Y-screenSize.Height/2);
    Rectangle parts = WorldToPart(new Rectangle(topLeft, screenSize));

    int yo, xo, pxo, pyo=0;
    for(int y=parts.Y; y<parts.Bottom; pyo+=PartHeight,y++)
    { pxo=0;
      for(int x=parts.X; x<parts.Right; pxo+=PartWidth,x++)
      { Partition part = GetPartition(x, y);
        if(part==null) continue;
        yo = pyo;

        Tile[,] tiles = part.RawTiles;
        for(int ty=0; ty<PartBlocksY; yo+=BlockHeight,ty++)
        { xo = pxo;
          for(int tx=0; tx<PartBlocksX; xo+=BlockWidth,tx++)
          { Tile tile = tiles[tx, ty];
            if(tile.Texture!=null)
            { GetTexture(tile.Texture).Bind();
              GL.glBegin(GL.GL_QUADS);
                GL.glTexCoord2f(0, 0); GL.glVertex2i(xo, yo);
                GL.glTexCoord2f(1, 0); GL.glVertex2i(xo+BlockWidth, yo);
                GL.glTexCoord2f(1, 1); GL.glVertex2i(xo+BlockWidth, yo+BlockHeight);
                GL.glTexCoord2f(0, 1); GL.glVertex2i(xo, yo+BlockHeight);
              GL.glEnd();
            }
          }
        }
      }
    }
  }

  public void Unload()
  { foreach(Partition p in parts.Values)
      if(p.RawTiles!=null)
        foreach(Tile t in p.RawTiles)
          if(t.Texture!=null && t.Texture.Texture!=null) t.Texture.Texture.Dispose();
    parts.Clear();
    tiles.Clear();
    mru.Clear();

    fsfile.Close();
    fsfile=null;

    basePath = null;
    bgColor  = Color.Transparent;
    name     = string.Empty;
  }

  const int BlockWidth=128, BlockHeight=64; // in world pixels
  const int PartBlocksX=2, PartBlocksY=2;
  const int PartWidth=PartBlocksX*BlockWidth, PartHeight=PartBlocksY*BlockHeight;
  const int MaxTiles=32*1024*1024/BlockWidth/BlockHeight/4; // 32 meg tile cache
  
  public enum PolyType { Solid, Water };
  class Polygon
  { public Polygon(PolyType type, GameLib.Mathematics.TwoD.Polygon poly) { this.type=type; this.poly=poly; }
    public GameLib.Mathematics.TwoD.Polygon Poly { get { return poly; } }
    public PolyType Type { get { return type; } }

    GameLib.Mathematics.TwoD.Polygon poly;
    PolyType type;
  }

  class CachedTexture
  { public CachedTexture(string filename) { Filename=filename; }
    public string Filename;
    public GLTexture2D Texture;
  }

  struct Tile
  { public Tile(Color c) { Texture=null; Color=c; }
    public Tile(string filename) { Texture = new CachedTexture(filename); Color = Color.FromArgb(0); }
    public string Filename { get { return Texture.Filename; } }
    public CachedTexture Texture;
    public Color Color;
  }

  class Partition
  { public Partition(Point coord) { this.coord=coord; }
    public ArrayList Objs  { get { if(objs==null) objs=new ArrayList(4); return objs; } }
    public ArrayList Polys { get { if(polys==null) polys=new ArrayList(2); return polys; } }
    public Tile[,]   Tiles { get { if(tiles==null) tiles=new Tile[PartWidth, PartHeight]; return tiles; } }

    public ArrayList RawObjs  { get { return objs; } }
    public ArrayList RawPolys { get { return polys; } }
    public Tile[,]   RawTiles { get { return tiles; } }

    public override bool Equals(object o)
    { if(!(o is Partition)) return false;
      return coord == ((Partition)o).coord;
    }

    public override int GetHashCode() { return coord.GetHashCode(); }

    ArrayList objs, polys;
    Tile[,] tiles;
    Point coord;
  }

  Partition GetPartition(int x, int y) { return GetPartition(new Point(x, y)); }
  Partition GetPartition(Point coord) { return (Partition)parts[coord]; }

  Partition GetPartitionW(int x, int y) { return GetPartition(WorldToPart(new Point(x, y))); }
  Partition GetPartitionW(Point coord) { return GetPartition(WorldToPart(coord)); }

  GLTexture2D GetTexture(CachedTexture ct)
  { LinkedList.Node node = (LinkedList.Node)tiles[ct.Filename];
    if(node!=null) // if found, move it to the front of the MRU list
    { mru.Remove(node);
      mru.Prepend(node);
    }
    else
    { ct.Texture = new GLTexture2D(new Surface(fsfile.GetStream(name), ImageType.PNG, false));
      ct.Texture.Bind();
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_NEAREST);
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_NEAREST);
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, GL.GL_CLAMP_TO_EDGE);
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, GL.GL_CLAMP_TO_EDGE);
      tiles[ct.Filename] = mru.Prepend(ct);
      UnloadOldTiles();
    }
    return ct.Texture;
  }

  Partition MakePartition(int x, int y) { return MakePartition(new Point(x, y)); }
  Partition MakePartition(Point coord)
  { object o = parts[coord];
    if(o!=null) return (Partition)o;
    Partition p = new Partition(coord);
    parts[coord] = p;
    return p;
  }

  Partition MakePartitionW(int x, int y) { return MakePartition(WorldToPart(new Point(x, y))); }
  Partition MakePartitionW(Point coord)  { return MakePartition(WorldToPart(coord)); }

  Point TileOffset(Point coord) // assumes tile coordinates are never negative
  { coord.X = coord.X%PartWidth  / BlockWidth;
    coord.Y = coord.Y%PartHeight / BlockHeight;
    return coord;
  }
  
  void UnloadOldTiles()
  { while(mru.Count>MaxTiles)
    { LinkedList.Node node = mru.Tail;
      CachedTexture ct = (CachedTexture)node.Data;
      ct.Texture.Dispose();
      ct.Texture = null;
      tiles.Remove(ct.Filename);
      mru.Remove(node);
    }
  }

  Point WorldToPart(Point coord)
  { coord.X = GLMath.FloorDiv(coord.X, PartWidth);
    coord.Y = GLMath.FloorDiv(coord.Y, PartHeight);
    return coord;
  }

  Rectangle WorldToPart(Rectangle rect)
  { rect.X = GLMath.FloorDiv(rect.X, PartWidth);
    rect.Y = GLMath.FloorDiv(rect.Y, PartHeight);
    rect.Width  = (rect.Width+PartWidth-1) / PartWidth;
    rect.Height = (rect.Height+PartHeight-1) / PartHeight;
    return rect;
  }

  Hashtable parts=new Hashtable(), tiles=new Hashtable();
  LinkedList mru=new LinkedList();
  FSFile  fsfile;
  string name, basePath;
  Color bgColor;
}

} // namespace Bimbo