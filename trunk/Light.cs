using System;
using System.Collections;
using SD=System.Drawing;
using GameLib.Mathematics.TwoD;

namespace Bimbo
{

public class Light
{ public int LitLength { get { return litlen; } }
  public Polygon[] LitShape { get { return litshape; } }

  public void CreateNgon(float radius, int sides)
  { float step=(float)(Math.PI*2/sides);
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

  public virtual float Falloff(Point point)
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
      foreach(World.Polygon wpoly in wpolys)
        for(int ei=0,elen=wpoly.Poly.Length; ei<elen; ei++)
        { Line edge = wpoly.Poly.GetEdge(ei);
          for(int npoly=newlit.Count-1; npoly>=0; npoly--)
          { Polygon tri = (Polygon)newlit[npoly];
            if(edge.WhichSide(tri[0])<=0) continue; // discount edges facing away
            LineIntersectInfo wall1 = edge.GetIntersection(tri.GetEdge(0));
            LineIntersectInfo wall2 = edge.GetIntersection(tri.GetEdge(2));
            if(wall1.OnBoth && wall2.OnBoth) { tri[1] = wall1.Point; tri[2] = wall2.Point; } // bisects triangle
            else if(wall1.OnBoth || wall2.OnBoth) // intersects just one wall
            { Point end = ei==elen-1 ? wpoly.Poly[0] : wpoly.Poly[ei+1];
              int ipi = tri.ConvexContains(edge.Start) ? 0 : tri.ConvexContains(end) ? 1 : -1;
              Point ip = ipi==-1 ? edge.LineIntersection(tri.GetEdge(1))
                                 : new Line(tri[0], ipi==0 ? edge.Start : end).LineIntersection(tri.GetEdge(1));
              if(!ip.Valid) continue;
              Point p2 = ipi==-1 ? ip : edge.GetPoint(ipi);
              if((wall1.OnBoth ? wall1 : wall2).Point==p2) continue;

              Polygon ntri = new Polygon(3);
              ntri.AddPoint(tri[0]);

              if(wall1.OnBoth)
              { ntri.AddPoint(wall1.Point);
                ntri.AddPoint(p2);
                tri[1] = ip;
              }
              else
              { ntri.AddPoint(p2);
                ntri.AddPoint(wall2.Point);
                tri[2] = ip;
              }

              newlit.Add(ntri);
            }
            else // intersects neither of the two walls
            { Point end = ei==elen-1 ? wpoly.Poly[0] : wpoly.Poly[ei+1];
              bool p1 = tri.ConvexContains(edge.Start), p2 = tri.ConvexContains(end);
              if(!p1 && !p2) continue; // line is entirely outside
              Point ip1 = tri.GetEdge(1).LineIntersection(p1 ? new Line(tri[0], edge.Start) : edge);
              Polygon ntri;
              if(ip1.Valid && ip1 != tri[2])
              { ntri = new Polygon(3);
                ntri.AddPoint(tri[0]);
                ntri.AddPoint(ip1);
                ntri.AddPoint(tri[2]);
                newlit.Add(ntri);
              }

              Point ip2 = tri.GetEdge(1).LineIntersection(p2 ? new Line(tri[0], end) : edge);
              if(ip2.Valid && ip2 != tri[1])
              { ntri = new Polygon(3);
                ntri.AddPoint(tri[0]);
                ntri.AddPoint(tri[1]);
                ntri.AddPoint(ip2);
                newlit.Add(ntri);
              }

              tri[1] = p2 ? end : ip2;
              tri[2] = p1 ? edge.Start : ip1;
            }
            if(tri[1]==tri[2]) newlit.RemoveAt(npoly);
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
  protected float radius;
  ArrayList newlit = new ArrayList(), wpolys = new ArrayList();
}

}