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
using GameLib.Mathematics;
using GameLib.Video;

namespace Bimbo
{

public class World
{ public string Name { get { return name; } }

  public void Load(System.IO.Stream stream) { Load(new List(stream)); }

  public void Load(List list)
  { Unload();
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
          Rectangle brect =
            WorldToPart(new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height));
          // TODO: finish me
        }
      }
      else if(child.Name=="layer") // FIXME: assumes layers are in order
      { List sub = child["tiles"];
        if(sub!=null)
          foreach(List tile in sub)
          { if(tile["zoom"].GetInt(0) != 1) continue;
            Point pos = tile["pos"].ToPoint();
            Partition part = GetPartition(pos);
            pos = TileOffset(pos);
            List color = tile["color"];
            if(color!=null) part.Tiles[pos.X, pos.Y].Color = color.ToColor();
            else part.Tiles[pos.X, pos.Y].Filename = tile.GetString(0);
          }
      }
      else if(child.Name=="level-options")
      { List opt = child["bgColor"];
        if(opt!=null) bgColor = opt.ToColor();
        opt = child["name"];
        if(opt!=null) name = opt.GetString(0);
      }
  }

  public void Unload()
  { foreach(Partition p in parts.Values)
      if(p.RawTiles!=null)
        foreach(Tile t in p.RawTiles)
          if(t.Texture!=null) t.Texture.Dispose();
    parts.Clear();
    
    bgColor = Color.Transparent;
    name    = string.Empty;
  }

  const int BlockWidth=128, BlockHeight=64; // in world pixels
  const int PartBlocksX=2, PartBlocksY=2;
  const int PartWidth=PartBlocksX*BlockWidth, PartHeight=PartBlocksY*BlockHeight;
  
  public enum PolyType { Solid, Water };
  class Polygon
  { public Polygon(PolyType type, GameLib.Mathematics.TwoD.Polygon poly) { this.type=type; this.poly=poly; }
    public GameLib.Mathematics.TwoD.Polygon Poly { get { return poly; } }
    public PolyType Type { get { return type; } }

    GameLib.Mathematics.TwoD.Polygon poly;
    PolyType type;
  }

  struct Tile
  { public string Filename;
    public GLTexture2D Texture;
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

  Partition GetPartition(Point coord)
  { object o = parts[coord];
    if(o!=null) return (Partition)o;
    Partition p = new Partition(coord);
    parts[coord] = p;
    return p;
  }

  Point TileOffset(Point coord) // assumes tiles coordinates are never negative
  { coord.X = coord.X%PartWidth  / BlockWidth;
    coord.Y = coord.Y%PartHeight / BlockHeight;
    return coord;
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

  Hashtable parts = new Hashtable();
  string name;
  Color bgColor;
}

} // namespace Bimbo