/*
BimboApp is a sample application that uses the Bimbo 2D platform game engine.
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
using Bimbo;
using GameLib.Input;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;

namespace Bimbo.Objects
{

public class Coin : BimboObject
{ public Coin(List list) : base(list) { sprite = Sprite.Load("coin.sps"); }

  public override void Render() { sprite.Render(pos, Anim.GetFrame(AnimPos)); }
  public override void Update() { }

  Sprite sprite;

  internal static Animation.Position AnimPos;
  internal static Animation Anim = new Animation(new Animation.Chunk(0,  8, 0.10f));
}

public class Player : BimboObject
{ public Player(List list) : base(list) { sprite = Sprite.Load("swarmie.png"); me=this; }

  public Rectangle Bounds
  { get
    { return new Rectangle(pos.X-sprite.Width*0.5f, pos.Y-sprite.Height*0.5f, sprite.Width, sprite.Height);
    }
  }

  public void AddForce(Vector force) { AddForce(force, new Point(0, 0)); } // apply a force to the object

  // apply a force to a point on the object, relative to the center of mass
  public void AddForce(Vector force, Point point) { this.force += force; }

  public void Friction()
  { 
  }

  public void Gravity() { AddForce(world.Gravity*(Mass*world.TimeDelta)); } // add gravity 

  // try to move according to the velocity. do collision detection with walls and other objects.
  public void Move()
  { partPolys.Clear();
    SD.Rectangle parts = World.WorldToPart(Bounds);
    for(int y=parts.Y; y<parts.Bottom; y++) // add all the unique polygons to partPolys
      for(int x=parts.X; x<parts.Right; x++)
      { World.Partition part = world.GetPartition(x, y);
        if(part==null) continue;
        ArrayList polys = part.RawPolys;
        if(polys==null) continue;
        foreach(World.Polygon poly in polys) if(!partPolys.Contains(poly)) partPolys.Add(poly);
      }
    vel = Move(vel*world.TimeDelta, 0) / world.TimeDelta;
  }

  public Vector Move(Vector vel, int initialCount)
  { World.LinePolyIntersection lpi = new World.LinePolyIntersection(), plpi;
    Line movement;
    int size = sprite.Width/2, count=initialCount, mini;

    do
    { movement=new Line(pos, vel); mini=-1; lpi.DistSqr=float.MaxValue;
      for(int i=0; i<partPolys.Count; i++)
      { World.Polygon poly = (World.Polygon)partPolys[i];
        if(poly.Intersection(movement, size, out plpi) && plpi.DistSqr<lpi.DistSqr) { lpi=plpi; mini=i; }
      }
      if(mini==-1) break;
      if(lpi.DistSqr>1) Move(lpi.IP-pos, count+1); // FIXME: with this, the parallel component gets added more than 1.0 times
      // cancel out the perpendicular component of the velocity
      vel -= lpi.Normal * (vel.Length * lpi.Normal.DotProduct(vel.Normal)); 
      if(vel.LengthSqr/world.TimeDelta<0.1f) return new Vector(); // set small velocities to zero
    } while(++count<50);
if(count>=50) throw new Exception("too many iterations");

    pos = movement.End;
    return vel;
  }

  public override void Render()
  { sprite.Render(pos, 0);

    float w = sprite.Width*0.5f, h=sprite.Height*0.5f;
    GL.glBindTexture(GL.GL_TEXTURE_2D, 0);
    GL.glColor(SD.Color.Red);
    GL.glBegin(GL.GL_LINE_LOOP);
      GL.glVertex2f(pos.X-w, pos.Y-h);
      GL.glVertex2f(pos.X+w, pos.Y-h);
      GL.glVertex2f(pos.X+w, pos.Y+h);
      GL.glVertex2f(pos.X-w, pos.Y+h);
    GL.glEnd();
    
    GL.glColor(SD.Color.White);
    GL.glBegin(GL.GL_LINES);
      GL.glVertex2f(pos.X, pos.Y);
      GL.glVertex2f(pos.X+vel.X, pos.Y+vel.Y);
    GL.glEnd();
  }

  public override void Update()
  { Gravity();
    float walk = WalkingAccel;

    if(walk==0) Friction(); // if we're not walking, slow us down using friction
    else
    { float vlen = vel.LengthSqr;
      // if we're moving faster than our maximum speed or walking in a direction opposite our motion,
      // apply friction to slow us down
      if(vlen > MaxWalkingSpeed*MaxWalkingSpeed || Math.Abs(Math.Sign(vlen)-Math.Sign(walk))==2) Friction();

      walk *= world.TimeDelta;
      if(Math.Sign(vlen)==Math.Sign(walk))       // if walking in the same direction as our motion, limit our
      { vlen = (float)Math.Sqrt(Math.Abs(vlen)); // speed increase so it doesn't put us over the maximum walk
        if(vlen>=MaxWalkingSpeed) walk=0;        // speed
        else walk = Math.Min(Math.Abs(walk), MaxWalkingSpeed-vlen) * Math.Sign(walk);
      }
      AddForce(RightVector * (walk*Mass));
    }

    vel += force/Mass; force = new Vector(); // apply all the forces on the object
    if(vel.LengthSqr > MaxSpeed*MaxSpeed) vel.Normalize(MaxSpeed); // limit to maximum speed
    if(vel.Y>=0 && Keyboard.Pressed(Key.Up)) vel.Y -= 250;
    
    Move(); // attempt to move
  }
  
  public Vector RightVector { get { return new Vector(1, 0); } }

  public float WalkingAccel
  { get { return (Keyboard.Pressed(Key.Left)?-400:0) + (Keyboard.Pressed(Key.Right)?400:0); }
  }

  protected Vector force;
  protected float Mass=100, MaxSpeed=3000, MaxWalkingSpeed=600, StopSpeed=16;

  Sprite sprite;

  public static Player me;
  static ArrayList partPolys = new ArrayList();
}

}