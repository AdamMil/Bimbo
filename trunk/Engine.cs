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

using System.Collections;
using GameLib;
using GameLib.Events;
using GameLib.Input;
using GameLib.Network;
using GameLib.Video;
using GameLib.Interop.OpenGL;

namespace Bimbo
{

public sealed class Engine
{ 
  public static World ActiveWorld
  { get { return world; }
    set
    { world=value;
      if(modeSet)
      { System.Drawing.Color c = world.BackColor;
        GL.glClearColor(c.R/255f, c.G/255f, c.B/255f, 1);
      }
    }
  }

  public static string DefaultObjectNamespace { get { return defObjNS; } set { defObjNS=value; } }
  public static System.Reflection.Assembly ObjectAssembly { get { return objAssem; } set { objAssem=value; } }

  public static System.Drawing.Size ScreenSize { get { return size; } }
  
  public static FSFile SpriteFile { get { return spriteFile; } set { spriteFile=value; } }
  public static string SpritePath
  { get { return spritePath; }
    set { spritePath = value==null ? null : GameLib.Utility.NormalizeDir(value); }
  }

  public static string WindowTitle
  { get { return title; }
    set
    { title=value;
      if(modeSet) WM.WindowTitle = title;
    }
  }

  public static void AddWorld(World w)
  { worlds.Add(w);
    if(world==null) ActiveWorld = w;
  }

  public static void Deinitialize()
  { foreach(World w in worlds) w.Unload();
    worlds.Clear();
    world = null;
    modeSet = false;
    Sprite.UnloadAll();
    if(spriteFile!=null)
    { spriteFile.Close();
      spriteFile = null;
    }
  }

  public static void EventLoop() { EventLoop(null); }
  public static void EventLoop(EventProcedure eventproc)
  { lastTime = Timing.Seconds;
    while(true)
    { if(!ProcessEvents(eventproc, false)) break;
      if(world!=null)
      { double time = Timing.Seconds, delta = System.Math.Min(time-lastTime, 0.1f);
        lastTime = time;
        world.Update(delta);
        Render();
      }
    }
  }
  
  public static void Initialize()
  { Events.Initialize();
    Input.Initialize();
    Video.Initialize();
    worlds = new ArrayList();
    if(objAssem==null) objAssem = System.Reflection.Assembly.GetCallingAssembly();
  }
  
  public static bool ProcessEvents() { return ProcessEvents(null, false); }
  public static bool ProcessEvents(bool wait) { return ProcessEvents(null, wait); }
  public static bool ProcessEvents(EventProcedure eventproc, bool wait)
  { Event e = wait ? Events.NextEvent() : Events.NextEvent(0);
    if(e!=null)
      do
      { if(eventproc!=null && !eventproc(e)) return false;
        Input.ProcessEvent(e);
        if(Keyboard.Pressed(Key.Escape) || e.Type==EventType.Quit) return false;
        if(e.Type==EventType.Exception) throw ((ExceptionEvent)e).Exception;
      } while((e=Events.NextEvent(0))!=null);
    return true;
  }

  public static void RemoveWorld(World w)
  { w.Unload();
    worlds.Remove(w);
    if(world==w) world=null;
  }

  public static void Render() { if(world!=null) Render(world, true); }
  public static void Render(bool flip) { if(world!=null) Render(world, flip); }
  public static void Render(World world) { Render(world, true); }
  public static void Render(World world, bool flip)
  { GL.glClear(GL.GL_COLOR_BUFFER_BIT);
    world.Render();
    if(flip) Video.Flip();
  }

  public static void SetMode(int width, int height)
  { Video.SetGLMode(width, height, 32);

    GL.glDisable(GL.GL_DITHER);
    GL.glEnable(GL.GL_BLEND);
    GL.glEnable(GL.GL_TEXTURE_2D);
    GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
    GL.glHint(GL.GL_PERSPECTIVE_CORRECTION_HINT, GL.GL_FASTEST);
    GL.glShadeModel(GL.GL_SMOOTH);

    GL.glViewport(0, 0, width, height);
    GLU.gluOrtho2D(0, width, height, 0);
    size = new System.Drawing.Size(width, height);
    modeSet = true;
    if(world!=null) ActiveWorld = world;
    WindowTitle = title;
  }

  static ArrayList worlds;
  static World world;
  static FSFile spriteFile;
  static System.Reflection.Assembly objAssem;
  static string title = "Bimbo App", defObjNS = "Bimbo.Objects", spritePath;
  static System.Drawing.Size size;
  static double lastTime;
  static bool modeSet;
}

} // namespace Bimbo