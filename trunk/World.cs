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
{ public World() { }
  public World(string path) { Load(path); }

  #region Constant, enum, and type definitions
  public const int BlockWidth=128, BlockHeight=64; // in world pixels
  public const int PartBlocksX=2, PartBlocksY=2;
  public const int PartWidth=PartBlocksX*BlockWidth, PartHeight=PartBlocksY*BlockHeight;
  public const int MaxTiles=32*1024*1024/BlockWidth/BlockHeight/4; // TODO: 32 meg tile cache -- make this dynamic

  public enum PolyType { Solid, Water };
  
  public struct LinePolyIntersection
  { public Point  IP;
    public Line   Line;
    public Vector Normal;
    public float  DistSqr;
  }

  public class Polygon
  { public Polygon(PolyType type, GameLib.Mathematics.TwoD.Polygon poly) { this.type=type; Poly=poly; }

    public Rectangle Bounds { get { return bounds; } }

    public GameLib.Mathematics.TwoD.Polygon Poly
    { get { return poly; }
      set
      { if(value==null) throw new ArgumentNullException("Poly");
        if(poly!=value)
        { poly=value;
          Update();
        }
      }
    }

    public PolyType Type { get { return type; } }

    public Vector GetEdgeNormal(int edge) { return normals[edge]; }

    // gets the specified edge of the polygon, assuming the polygon has been inflated by 'size'
    public Line GetInflatedEdge(int edge, float size)
    { Line line = poly.GetEdge(edge);
      line.Start += normals[edge]*size;
      return line;
    }

    // Returns the point of intersection of 'line' with the polygon inflated by 'size', or Point.Invalid
    // if no collision occurred
    public bool Intersection(Line line, float size, out LinePolyIntersection lpi)
    { const float epsilon = 0.1f;
      if(!line.Intersects(Bounds.Inflated(size, size))) goto nothit;

      unsafe
      { Line* lines = stackalloc Line[poly.Length];
        float* sdists = stackalloc float[poly.Length];
        float* edists = stackalloc float[poly.Length];
        Point end = line.End, ip = new Point();
        float mind = float.MaxValue;
        int mini = -1, len=poly.Length;
        for(int i=0; i<len; i++)
        { lines[i] = GetInflatedEdge(i, size); // inflate the polygon by 'size'
          sdists[i] = normals[i].DotProduct(line.Start - lines[i].Start);
          edists[i] = normals[i].DotProduct(end - lines[i].Start);
        }

        for(int i=0; i<len; i++)
        { float sd = sdists[i], ed = edists[i];
          if(sd<epsilon && sd>-epsilon) // this special case (we're already touching it) helps prevent buildup
          { for(int j=0; j<len; j++) if(j!=i && sdists[j]>=epsilon) goto nope;  // of floating-point errors
            for(int j=0; j<len; j++) if(edists[j]>-epsilon) goto nothit;
            lpi.IP   = line.Start;
            lpi.Line = lines[i];
            lpi.Normal = normals[i];
            lpi.DistSqr = 0;
            return true;
          }
          if(sd>=epsilon && ed<=-epsilon) // 'line' straddles a clipping line
          { Point pip = line.Start + line.Vector*(sd/(sd-ed));//line.LineIntersection(lines[i]);
            float dist = pip.DistanceSquaredTo(line.Start);
            if(dist<mind)
            { for(int j=0; j<len; j++) if(j!=i && normals[j].DotProduct(pip - lines[j].Start)>epsilon) goto nope;
              ip=pip; mini=i; mind=dist;
            }
          }
          nope:;
        }
        if(mini==-1) goto nothit;
        lpi.IP      = ip + normals[mini]*0.5f;
        lpi.Line    = lines[mini];
        lpi.Normal  = normals[mini];
        lpi.DistSqr = mind;
        return true;
      }
      
      nothit:
      unsafe { fixed(LinePolyIntersection* p=&lpi) { } } // pacify the compiler (no, we didn't assign to lpi)
      return false;
    }

    public void Update() // precalculate the bounding box and the edge normals
    { if(!poly.IsConvex() || !poly.IsClockwise())
        throw new ArgumentException("The polygon must be convex and defined in a clockwise manner.");
      if(normals==null || normals.Length!=poly.Length) normals = new Vector[poly.Length];
      for(int i=0; i<poly.Length; i++) normals[i] = poly.GetEdge(i).Vector.CrossVector.Normal;
      bounds = poly.GetBounds();
    }

    GameLib.Mathematics.TwoD.Polygon poly;
    Vector[] normals;
    Rectangle bounds;
    PolyType type;
  }

  public class CachedTexture
  { public CachedTexture(string filename) { Filename=filename; }
    public string Filename;
    public GLTexture2D Texture;
  }

  public struct Tile
  { public Tile(SD.Color c) { Texture=null; Color=c; }
    public Tile(string filename) { Texture = new CachedTexture(filename); Color = SD.Color.FromArgb(0); }
    public string Filename { get { return Texture.Filename; } }
    public CachedTexture Texture;
    public SD.Color Color;
  }

  public class Partition
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
  #endregion

  public SD.Color BackColor { get { return bgColor; } }
  public Camera Camera
  { get { return cam; }
    set
    { if(value==null) throw new ArgumentNullException("Camera");
      cam = value;
    }
  }
  public int Frame { get { return frame; } }
  public Vector Gravity { get { return new Vector(0, 250); } }
  public string Name { get { return levelName; } }
  public float Time { get { return time; } }
  public float TimeDelta { get { return timeDelta; } }

  public Partition GetPartition(int x, int y) { return GetPartition(new Point(x, y)); }
  public Partition GetPartition(Point coord) { return GetPartition(coord.ToPoint()); }
  public Partition GetPartition(SD.Point coord) { return (Partition)parts[coord]; }

  public Partition GetPartitionW(int x, int y) { return GetPartition(WorldToPart(new Point(x, y))); }
  public Partition GetPartitionW(Point coord) { return GetPartition(WorldToPart(coord)); }
  public Partition GetPartitionW(SD.Point coord) { return GetPartition(WorldToPart(new Point(coord))); }

  public GLTexture2D GetTexture(CachedTexture ct)
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

  public void Load(string path)
  { path = GameLib.Utility.NormalizeDir(path);
    Load(path, new List(System.IO.File.Open(path+"definition", System.IO.FileMode.Open, System.IO.FileAccess.Read)));
  }

  public void Load(string path, List list)
  { path = GameLib.Utility.NormalizeDir(path);
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
        { if(!p.IsClockwise()) p.Reverse();
          Polygon poly = new Polygon(type, p);
          SD.Rectangle brect = WorldToPart(poly.Poly.GetBounds()); // TODO: maybe replace next 3 lines with foreach, using iterator??
          for(int y=brect.Y; y<brect.Bottom; y++)
            for(int x=brect.X; x<brect.Right; x++)
              MakePartition(x, y).Polys.Add(poly); // FIXME: this adds to too many partitions
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
          { BimboObject o = BimboObject.CreateObject(this, obj);
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

    fsfile = new FSFile(path+"images.fsf", System.IO.FileAccess.Read);
    basePath = path;
    
    OnLoad();
  }

  public Partition MakePartition(int x, int y) { return MakePartition(new SD.Point(x, y)); }
  public Partition MakePartition(SD.Point coord)
  { object o = parts[coord];
    if(o!=null) return (Partition)o;
    Partition p = new Partition(coord);
    parts[coord] = p;
    return p;
  }

  public Partition MakePartitionW(int x, int y) { return MakePartition(WorldToPart(new Point(x, y))); }
  public Partition MakePartitionW(SD.Point coord) { return MakePartition(WorldToPart(new Point(coord))); }
  public Partition MakePartitionW(Point coord) { return MakePartition(WorldToPart(coord)); }

  protected virtual void OnLoad() { }
  protected virtual void OnUnload() { }
  
  public virtual void Render()
  { Point topLeft = Camera.TopLeft;
    SD.Rectangle parts = WorldToPart(new Rectangle(topLeft.X, topLeft.Y,
                                                   Engine.ScreenSize.Width, Engine.ScreenSize.Height));

    GL.glPushMatrix();
    GL.glTranslatef(-topLeft.X, -topLeft.Y, 0);

    for(int y=parts.Y; y<parts.Bottom; y++)
      for(int x=parts.X; x<parts.Right; x++)
      { Partition part = GetPartition(x, y);
        if(part!=null) part.ObjIndex = 0;
      }

    for(int layer=0; layer<numLayers; layer++)
    { float yo, xo, pxo, pyo=parts.Y*PartHeight;
      for(int y=parts.Y; y<parts.Bottom; pyo+=PartHeight,y++)
      { pxo=parts.X*PartWidth;
        for(int x=parts.X; x<parts.Right; pxo+=PartWidth,x++)
        { Partition part = GetPartition(x, y);
          if(part==null) continue;

          Tile[,] tiles = part.GetTiles(layer, false);
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
                    GL.glTexCoord2f(0, 0); GL.glVertex2f(xo, yo);
                    GL.glTexCoord2f(1, 0); GL.glVertex2f(xo+BlockWidth, yo);
                    GL.glTexCoord2f(1, 1); GL.glVertex2f(xo+BlockWidth, yo+BlockHeight);
                    GL.glTexCoord2f(0, 1); GL.glVertex2f(xo, yo+BlockHeight);
                  GL.glEnd();
                }
                else if(tile.Color.A!=0)
                { GL.glBindTexture(GL.GL_TEXTURE_2D, 0);
                  GL.glColor(tile.Color);
                  GL.glBegin(GL.GL_QUADS);
                    GL.glVertex2f(xo, yo);
                    GL.glVertex2f(xo+BlockWidth, yo);
                    GL.glVertex2f(xo+BlockWidth, yo+BlockHeight);
                    GL.glVertex2f(xo, yo+BlockHeight);
                  GL.glEnd();
                }
              }
            }
          }
        }
      }
      
      for(int y=parts.Y; y<parts.Bottom; y++)
        for(int x=parts.X; x<parts.Right; x++)
        { Partition part = GetPartition(x, y);
          if(part==null) continue;
          ArrayList objs = part.RawObjs;
          if(objs!=null)
            for(; part.ObjIndex<objs.Count; part.ObjIndex++)
            { BimboObject obj = (BimboObject)objs[part.ObjIndex];
              if(obj.Layer!=layer) break;
              obj.Render();
            }
        }

      GL.glBindTexture(GL.GL_TEXTURE_2D, 0);
      if(layer==numLayers-1)
        for(int y=parts.Y; y<parts.Bottom; y++)
          for(int x=parts.X; x<parts.Right; x++)
          { Partition part = GetPartition(x, y);
            if(part==null) continue;
            ArrayList polys = part.RawPolys;
            if(polys!=null)
            { foreach(Polygon poly in polys)
              { /*GL.glColor(16, SD.Color.Magenta);
                GL.glBegin(GL.GL_POLYGON);
                for(int i=0; i<poly.Poly.Length; i++) GL.glVertex2f(poly.Poly[i].X, poly.Poly[i].Y);
                GL.glEnd();*/
                GL.glColor(SD.Color.Magenta);
                GL.glBegin(GL.GL_LINE_LOOP);
                for(int i=0; i<poly.Poly.Length; i++) GL.glVertex2f(poly.Poly[i].X, poly.Poly[i].Y);
                GL.glEnd();
              }
              GL.glColor(32, SD.Color.LightGreen);
              
              foreach(Polygon poly in polys)
              { Rectangle rect = poly.Bounds.Inflated(16, 16);
                GL.glBegin(GL.GL_LINE_LOOP);
                  GL.glVertex2f(rect.X, rect.Y);
                  GL.glVertex2f(rect.Right, rect.Y);
                  GL.glVertex2f(rect.Right, rect.Bottom);
                  GL.glVertex2f(rect.X, rect.Bottom);
                GL.glEnd();
              }
            }
          }
    }
    
    GL.glPopMatrix();
  }

  public SD.Point TileOffset(SD.Point coord) // assumes tile coordinates are never negative
  { coord.X = coord.X%PartWidth  / BlockWidth;
    coord.Y = coord.Y%PartHeight / BlockHeight;
    return coord;
  }

  public virtual void Update(float timeDelta)
  { time += timeDelta;
    this.timeDelta = timeDelta;
    cam.Update(timeDelta);

    ArrayList moved = null;
    foreach(Partition part in parts.Values)
    { ArrayList objs = part.RawObjs;
      if(objs==null) continue;
      for(int i=0; i<objs.Count; i++)
      { BimboObject obj = (BimboObject)objs[i];
        obj.Update();
        SD.Point npc = WorldToPart(obj.Pos);
        if(obj.PartCoords != npc)
        { obj.PartCoords = npc;
          objs.RemoveAt(i--);
          if(moved==null) moved=new ArrayList();
          moved.Add(obj);
        }
      }
    }

    if(moved!=null) foreach(BimboObject obj in moved) MakePartition(obj.PartCoords).Objs.Add(obj);
  }

  public void Unload()
  { OnUnload();
    foreach(Partition p in parts.Values)
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
    numLayers = frame = 0;
    time      = 0;
  }

  public void UnloadOldTiles()
  { while(mru.Count>MaxTiles)
    { LinkedList.Node node = mru.Tail;
      CachedTexture ct = (CachedTexture)node.Data;
      ct.Texture.Dispose();
      ct.Texture = null;
      tiles.Remove(ct.Filename);
      mru.Remove(node);
    }
  }

  public static SD.Point WorldToPart(Point coord)
  { return new SD.Point((int)Math.Floor(coord.X/PartWidth), (int)Math.Floor(coord.Y/PartHeight));
  }

  public static SD.Rectangle WorldToPart(Point p1, Point p2)
  { Point topLeft = new Point(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y));
    return WorldToPart(new Rectangle(topLeft.X, topLeft.Y, Math.Max(p1.X, p2.X)-topLeft.X,
                                     Math.Max(p1.Y, p2.Y)-topLeft.Y));
  }

  public static SD.Rectangle WorldToPart(Rectangle rect)
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
  float time, timeDelta;
  int numLayers, frame;
}

} // namespace Bimbo