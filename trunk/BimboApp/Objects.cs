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
using Bimbo;
using GameLib.Input;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;

namespace Bimbo.Objects
{

public class Coin : BimboObject
{ public Coin(List list) : base(list) { sprite = Sprite.Load("coin.sps"); }

  public override void Render() { DrawSprite(sprite, Anim.GetFrame(AnimPos)); }
  public override void Update() { }

  Sprite sprite;

  internal static Animation.Position AnimPos;
  internal static Animation Anim = new Animation(new Animation.Chunk(0,  8, 0.10f));
}

public class Player : BimboObject
{ public Player(List list) : base(list) { sprite = Sprite.Load("swarmie.png"); me=this; }

  public override void Render() { DrawSprite(sprite, 0); }

  public override void Update()
  { int accel = ;
    if(vel.X!=0 && (accel==0 || Math.Abs(Math.Sign(accel)-Math.Sign(vel.X))==2))
    { vel.X = (Math.Abs(vel.X) - friction*weight*world.TimeDelta) * Math.Sign(vel.X);
      if(Math.Abs(vel.X)<StopSpeed) vel.X=0;
      Console.WriteLine(vel.X);
    }
    if(accel!=0)
      vel.X += Math.Sign(accel) * Math.Min(Math.Abs(accel), MaxSpeed-Math.Abs((int)vel.X)) * world.TimeDelta;

    pos += vel * world.TimeDelta;
  }
  
  public float MaxWalkingSpeed { get { return 600; } }

  public float WalkingAccel
  { get { return (Keyboard.Pressed(Key.Left)?-Accel:0) + (Keyboard.Pressed(Key.Right)?Accel:0); }
  }
  
  public float 
  public const int StopSpeed=16, Accel=1000, MaxSpeed=600;

  Sprite sprite;

  public static Player me;
}

}