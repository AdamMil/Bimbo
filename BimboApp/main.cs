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
using GameLib.Mathematics.TwoD;

namespace BimboApp
{

class App
{ 
  static void Main()
  { Engine.Initialize();
    Engine.SpritePath = @"c:\games\swarm3\images\sprites";
    Engine.WindowTitle = "Bimbo Test";
    Engine.SetMode(800, 600);
    
    World world = new BimboAppWorld(@"C:\code\Smarm\data\test");
    world.Camera.SetDestination(Bimbo.Objects.Player.me);
    world.Camera.Current = world.Camera.Destination;
    Engine.AddWorld(world);

    try { Engine.EventLoop(); }
    finally { Engine.Deinitialize(); }
  }
}

}