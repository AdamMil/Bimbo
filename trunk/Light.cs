using System;
using System.Collections;
using SD=System.Drawing;
using GameLib.Mathematics.TwoD;

namespace Bimbo
{

public class Light
{ public int LitLength { get { return litlen; } }
  public Polygon[] LitShape { get { return litshape; } }

  public void CreateNgon(double radius, int sides)
  { double step=Math.PI*2/sides;
    Vector vec=new Vector(radius, 0);
    unsafe
    { Point* points = stackalloc Point[sides];
      for(int i=0; i<sides; i++) points[i] = vec.Rotated(step*i).ToPoint();
      shape = new Polygon[sides];
      for(int i=0; i<sides; i++)
      { shape[i] = new Polygon(3);
        shape[i].AddPoint(0, 0);
        shape[i].AddPoint(points[i==0 ? sides-1 : i-1]);
        shape[i].AddPoint(points[i]);
      }
    }
    this.radius = radius;
    ShapeUpdated();
  }

  public virtual double Falloff(Point point)
  { if(litlen==0) throw new InvalidOperationException("The light has no shape or has not been calculated yet!");
    return 1-point.DistanceTo(litshape[0][0])/radius;
  }

  public void Recalculate(World world, Point pos)
  { if(shape==null) { litshape=null; litlen=0; }
    else
    { newlit.Clear(); wpolys.Clear();

      SD.Rectangle parts;
      { Rectangle bounds = this.bounds;
        bounds.Offset(pos.X, pos.Y);
        parts = World.WorldToPart(bounds);
      }

      for(int i=0; i<shape.Length; i++) // the shape is stored relative to 0,0, so we begin by offsetting
      { Polygon poly = (Polygon)shape[i].Clone(); // copying it to 'newlit', offset by the position
        poly.Offset(pos.X, pos.Y);
        newlit.Add(poly);
      }

      // create an array containing the unique polygons
      for(int y=parts.Y; y<parts.Bottom; y++)
        for(int x=parts.X; x<parts.Right; x++)
        { World.Partition part = world.GetPartition(x, y);
          if(part==null) continue;
          ArrayList polys = part.RawPolys;
          if(polys==null) continue;
          foreach(World.Polygon wpoly in polys) if(! wpolys.Contains(wpoly)) wpolys.Add(wpoly);
        }

      // TODO: optimize edge order and use knowledge about edge connections
      // to reduce the number of generated triangles
      const double epsilon=0.001f;
      foreach(World.Polygon wpoly in wpolys)
        for(int ei=0,elen=wpoly.Poly.Length; ei<elen; ei++)
        { Line edge = wpoly.Poly.GetEdge(ei);
          for(int npoly=newlit.Count-1; npoly>=0; npoly--)
          { Polygon tri = (Polygon)newlit[npoly];
            if(edge.WhichSide(tri[0])<=0) continue;    // discount edges facing away
            Line clip = edge.ConvexIntersection(tri);  // clip the edge to the polygon
            if(!clip.Valid) continue;                  // discount edges that don't actually intersect
            Point start = clip.Start, end = clip.End;

            if(tri.GetEdge(0).DistanceTo(end) < -epsilon)
            { Point ip = tri.GetEdge(1).LineIntersection(new Line(tri[0], end));
              if(ip.Valid) newlit.Add(new Polygon(tri[0], tri[1], ip));
            }
            if(tri.GetEdge(2).DistanceTo(start) < -epsilon)
            { Point ip = tri.GetEdge(1).LineIntersection(new Line(tri[0], start));
              if(ip.Valid) newlit.Add(new Polygon(tri[0], ip, tri[2]));
            }
            tri[1]=end; tri[2]=start;
          }
        }
      if(litshape==null || newlit.Count>litshape.Length) litshape = new Polygon[newlit.Count];
      newlit.CopyTo(litshape);
      litlen = newlit.Count;
    }
  }

  protected void ShapeUpdated()
  { if(shape==null || shape.Length==0) bounds = new Rectangle();
    else
    { bounds = shape[0].GetBounds();
      for(int i=1; i<shape.Length; i++) bounds.Unite(shape[i].GetBounds());
    }
  }

  protected Rectangle bounds;
  protected Polygon[] shape, litshape;
  protected int litlen;
  protected double radius;
  ArrayList newlit = new ArrayList(), wpolys = new ArrayList();
}

}