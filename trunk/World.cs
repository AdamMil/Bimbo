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
using SD=System.Drawing;
using System.Collections;
using GameLib.Collections;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;
using GameLib.Video;

namespace Bimbo
{

public class World
{ public SD.Color BackColor { get { return bgColor; } }
  public Camera Camera { get { return cam; } }
  public string Name { get { return levelName; } }
  public float TimeDelta { get { return timeDelta; } }

  // TODO: add support for rotation and object tracking (?)

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
          SD.Rectangle brect = WorldToPart(poly.Poly.GetBounds()); // TODO: maybe replace next 3 lines with foreach, using iterator??
          for(int y=brect.Y; y<brect.Bottom; y++)
            for(int x=brect.X; x<brect.Right; x++)
              MakePartition(x, y).Polys.Add(poly);
        }
      }
      else if(child.Name=="layer") // FIXME: assumes layers are in order
      { int layer = child.GetInt(0);
        if(child.Length>1 && layer>=numLayers) numLayers=layer+1;

        List sub = child["tiles"];
        if(sub!=null)
          foreach(List tile in sub)
          { if(tile["zoom"].GetInt(0) != 4) continue;
            SD.Point pos = tile["pos"].ToPoint();
            Partition part = MakePartitionW(pos);
            pos = TileOffset(pos);
            List color = tile["color"];
            part.GetTiles(layer, true)[pos.X, pos.Y] =
              color==null ? new Tile(tile.GetString(0)) : new Tile(color.ToColor());
          }

        sub = child["objects"];
        if(sub!=null)
          foreach(List obj in sub)
          { BimboObject o = BimboObject.CreateObject(obj);
            o.Layer = layer;
            Partition part = MakePartitionW(o.Pos);
            part.Objs.Add(o);
          }
      }
      else if(child.Name=="level-options")
      { List opt = child["bgColor"];
        if(opt!=null) bgColor = opt.ToColor();
        opt = child["name"];
        if(opt!=null) levelName = opt.GetString(0);
      }

    fsfile = new FSFile(path+"images.fsf");
    basePath = path;
  }

  public void Render(SD.Size screenSize)
  { SD.Point topLeft = new SD.Point((int)Math.Round(cam.Current.X)-screenSize.Width/2,
                                    (int)Math.Round(cam.Current.Y)-screenSize.Height/2);
    SD.Rectangle parts = WorldToPart(new Rectangle(topLeft.X, topLeft.Y, screenSize.Width, screenSize.Height));

    int yo, xo, pxo, pyo=parts.Y*PartHeight - topLeft.Y;
    for(int y=parts.Y; y<parts.Bottom; pyo+=PartHeight,y++)
    { pxo=parts.X*PartWidth - topLeft.X;
      for(int x=parts.X; x<parts.Right; pxo+=PartWidth,x++)
      { Partition part = GetPartition(x, y);
        if(part==null) continue;
        part.ObjIndex = 0;

        for(int layer=0; layer<numLayers; layer++)
        { Tile[,] tiles = part.GetTiles(layer, false);
          if(tiles!=null)
          { yo = pyo;
            for(int ty=0; ty<PartBlocksY; yo+=BlockHeight,ty++)
            { xo = pxo;
              for(int tx=0; tx<PartBlocksX; xo+=BlockWidth,tx++)
              { Tile tile = tiles[tx, ty];
                if(tile.Texture!=null)
                { GL.glColor(SD.Color.White);
                  GetTexture(tile.Texture).Bind();
                  GL.glBegin(GL.GL_QUADS);
                    GL.glTexCoord2f(0, 0); GL.glVertex2i(xo, yo);
                    GL.glTexCoord2f(1, 0); GL.glVertex2i(xo+BlockWidth, yo);
                    GL.glTexCoord2f(1, 1); GL.glVertex2i(xo+BlockWidth, yo+BlockHeight);
                    GL.glTexCoord2f(0, 1); GL.glVertex2i(xo, yo+BlockHeight);
                  GL.glEnd();
                }
                else if(tile.Color.A!=0)
                { GL.glBindTexture(GL.GL_TEXTURE_2D, 0);
                  GL.glColor(tile.Color);
                  GL.glBegin(GL.GL_QUADS);
                    GL.glVertex2i(xo, yo);
                    GL.glVertex2i(xo+BlockWidth, yo);
                    GL.glVertex2i(xo+BlockWidth, yo+BlockHeight);
                    GL.glVertex2i(xo, yo+BlockHeight);
                  GL.glEnd();
                }
              }
            }
          }
          
          ArrayList objs = part.RawObjs;
          if(objs!=null)
            for(; part.ObjIndex<objs.Count; part.ObjIndex++)
            { BimboObject obj = (BimboObject)objs[part.ObjIndex];
              if(obj.Layer!=layer) goto doneWithObjs;
              obj.Render(cam);
            }
          doneWithObjs:;
        }
      }
    }
  }

  public void Unload()
  { foreach(Partition p in parts.Values)
      if(p.RawTiles!=null)
        foreach(Tile[,] ta in p.RawTiles)
          if(ta!=null)
            foreach(Tile t in ta)
              if(t.Texture!=null && t.Texture.Texture!=null) t.Texture.Texture.Dispose();

    parts.Clear();
    tiles.Clear();
    mru.Clear();

    if(fsfile!=null)
    { fsfile.Close();
      fsfile=null;
    }

    basePath  = null;
    bgColor   = SD.Color.Black;
    levelName = string.Empty;
    numLayers = 0;
  }

  public void Update(float timeDelta)
  { this.timeDelta = timeDelta;
    cam.Update(timeDelta);
    foreach(Partition part in parts.Values)
    { ArrayList objs = part.RawObjs;
      if(objs!=null) foreach(BimboObject obj in objs) obj.Update(this);
    }
  }

  const int BlockWidth=128, BlockHeight=64; // in world pixels
  const int PartBlocksX=2, PartBlocksY=2;
  const int PartWidth=PartBlocksX*BlockWidth, PartHeight=PartBlocksY*BlockHeight;
  const int MaxTiles=32*1024*1024/BlockWidth/BlockHeight/4; // TODO: 32 meg tile cache -- make this dynamic

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
  { public Tile(SD.Color c) { Texture=null; Color=c; }
    public Tile(string filename) { Texture = new CachedTexture(filename); Color = SD.Color.FromArgb(0); }
    public string Filename { get { return Texture.Filename; } }
    public CachedTexture Texture;
    public SD.Color Color;
  }

  class Partition
  { public Partition(SD.Point coord) { this.coord=coord; }
    public ArrayList Objs  { get { if(objs==null) objs=new ArrayList(4); return objs; } }
    public ArrayList Polys { get { if(polys==null) polys=new ArrayList(2); return polys; } }

    public ArrayList RawObjs  { get { return objs; } }
    public ArrayList RawPolys { get { return polys; } }
    public Tile[][,] RawTiles { get { return tiles; } }

    public Tile[,] GetTiles(int layer, bool create)
    { if(tiles==null)
      { if(!create) return null;
        tiles = new Tile[Math.Max(layer+1, 8)][,];
      }
      if(tiles[layer]==null)
      { if(!create) return null;
        if(layer>=tiles.Length)
        { Tile[][,] narr = new Tile[Math.Max(layer+1, tiles.Length*2)][,];
          Array.Copy(tiles, narr, tiles.Length);
          tiles = narr;
        }
        tiles[layer] = new Tile[PartBlocksX, PartBlocksY];
      }
      return tiles[layer];
    }

    public override bool Equals(object o)
    { if(!(o is Partition)) return false;
      return coord == ((Partition)o).coord;
    }

    public override int GetHashCode() { return coord.GetHashCode(); }

    public int ObjIndex;

    ArrayList objs, polys;
    Tile[][,] tiles;
    SD.Point coord;
  }

  Partition GetPartition(int x, int y) { return GetPartition(new Point(x, y)); }
  Partition GetPartition(Point coord) { return GetPartition(coord.ToPoint()); }
  Partition GetPartition(SD.Point coord) { return (Partition)parts[coord]; }

  Partition GetPartitionW(int x, int y) { return GetPartition(WorldToPart(new Point(x, y))); }
  Partition GetPartitionW(Point coord) { return GetPartition(WorldToPart(coord)); }
  Partition GetPartitionW(SD.Point coord) { return GetPartition(WorldToPart(new Point(coord))); }

  GLTexture2D GetTexture(CachedTexture ct)
  { LinkedList.Node node = (LinkedList.Node)tiles[ct.Filename];
    if(node!=null) // if found, move it to the front of the MRU list
    { mru.Remove(node);
      mru.Prepend(node);
    }
    else
    { ct.Texture = new GLTexture2D(new Surface(fsfile.GetStream(ct.Filename), ImageType.PNG, false));
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

  Partition MakePartition(int x, int y) { return MakePartition(new SD.Point(x, y)); }
  Partition MakePartition(SD.Point coord)
  { object o = parts[coord];
    if(o!=null) return (Partition)o;
    Partition p = new Partition(coord);
    parts[coord] = p;
    return p;
  }

  Partition MakePartitionW(int x, int y) { return MakePartition(WorldToPart(new Point(x, y))); }
  Partition MakePartitionW(SD.Point coord) { return MakePartition(WorldToPart(new Point(coord))); }
  Partition MakePartitionW(Point coord) { return MakePartition(WorldToPart(coord)); }

  SD.Point TileOffset(SD.Point coord) // assumes tile coordinates are never negative
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

  SD.Point WorldToPart(Point coord)
  { return new SD.Point((int)Math.Floor(coord.X/PartWidth), (int)Math.Floor(coord.Y/PartHeight));
  }

  SD.Rectangle WorldToPart(Rectangle rect)
  { return new SD.Rectangle((int)Math.Floor(rect.X/PartWidth), (int)Math.Floor(rect.Y/PartHeight),
                            (int)Math.Ceiling((rect.Width+(PartWidth-1))/PartWidth),
                            (int)Math.Ceiling((rect.Height+(PartHeight-1))/PartHeight));
  }

  Hashtable parts=new Hashtable(), tiles=new Hashtable();
  LinkedList mru=new LinkedList();
  FSFile  fsfile;
  Camera cam = new Camera();
  string levelName, basePath;
  SD.Color bgColor;
  float timeDelta;
  int numLayers;
}

} // namespace Bimbo