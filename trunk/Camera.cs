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
using System.Collections;
using GameLib.Mathematics.TwoD;

namespace Bimbo
{

// TODO: add support for rotation?
public class Camera
{ internal Camera() { }

  public Point Destination
  { get { return tracking==null ? dpoint : tracking.Pos; }
    set { tracking=null; dpoint=value; }
  }

  public Point TopLeft
  { get { return new Point(Current.X-Engine.ScreenSize.Width/2, Current.Y-Engine.ScreenSize.Height/2); }
  }

  public void Push(Point pt)
  { points.Push(tracking==null ? (object)dpoint : (object)tracking);
    dpoint=pt;
  }

  public void Pop() { SetDestination(points.Pop()); }
  
  public void SetDestination(object obj)
  { if(obj is BimboObject) tracking = (BimboObject)obj;
    else dpoint = (Point)obj;
  }

  public Point Current;

  internal void Update(float timeDelta)
  { Vector diff = Destination-Current;
    if(diff.LengthSqr<=9) Current=Destination;
    else
    { if(diff.LengthSqr>640000) diff.Normalize(800);
      Current += diff * (timeDelta * 4);
    }
  }

  Stack points = new Stack();
  BimboObject tracking;
  Point dpoint;
}

} // namespace Bimbo