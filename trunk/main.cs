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

// TODO: break the important out of here so that Bimbo can be a library!

using System;
using Bimbo;
using GameLib;
using GameLib.Events;
using GameLib.Input;
using GameLib.Video;
using GameLib.Interop.OpenGL;

namespace BimboApp
{

class App
{ 
  static void Deinitialize()
  { world.Unload();
  }

  static void Initialize()
  { Video.Initialize();
    Video.SetGLMode(800, 600, 32);

    GL.glDisable(GL.GL_DITHER);
    GL.glEnable(GL.GL_BLEND);
    GL.glEnable(GL.GL_TEXTURE_2D);
    GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
    GL.glHint(GL.GL_PERSPECTIVE_CORRECTION_HINT, GL.GL_FASTEST);
    GL.glShadeModel(GL.GL_FLAT);

    GL.glViewport(0, 0, 800, 600);
    GLU.gluOrtho2D(0, 800, 600, 0);
  
    Events.Initialize();
    Input.Initialize();
  }

  static void Main()
  { Initialize();
    world.Load(@"C:\code\Smarm\data\test");
    world.Camera.Current = new GameLib.Mathematics.TwoD.Point(-300, -300);
    world.Camera.Destination = new GameLib.Mathematics.TwoD.Point(4000, 2100);
    System.Drawing.Color c = world.BackColor;
    GL.glClearColor(c.R/255f, c.G/255f, c.B/255f, 1);

    float lastTime = (float)Timing.Seconds;
    try
    { while(true)
      { Event e;
        while((e=Events.NextEvent(0))!=null)
        { Input.ProcessEvent(e);
          if(Keyboard.Pressed(Key.Escape) || e is QuitEvent) goto done;
          if(e is ExceptionEvent) throw ((ExceptionEvent)e).Exception;
        }
        float time = (float)Timing.Seconds, delta = time-lastTime;
        lastTime = time;
        world.Update(delta);

        GL.glClear(GL.GL_COLOR_BUFFER_BIT);
        world.Render(Video.DisplaySurface.Size);
        Video.Flip();
      }
      done:;
    }
    finally { Deinitialize(); }
  }
  
  static World world = new World();
}

}