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
using System.IO;
using System.Collections;
using GameLib.Video;

namespace Bimbo
{

public class Sprite : IDisposable
{ public Sprite(string fileName)
  { if(fileName.ToLower().EndsWith(".sps")) Load(new List(GetStream(fileName)));
    else Load(new Surface(GetStream(fileName)));
  }
  public Sprite(List list) { Load(list); }

  public Sprite(string fileName, int elementWidth) { Load(fileName, elementWidth); }
  public Sprite(Surface surface) { Load(surface, surface.Width); }
  public Sprite(Surface surface, int elementWidth) { Load(surface, elementWidth); }

  ~Sprite() { Dispose(true); }
  public void Dispose() { Dispose(false); GC.SuppressFinalize(this); }

  public static Sprite Load(string fileName)
  { WeakReference wr = (WeakReference)sprites[fileName];
    Sprite sprite;
    if(wr!=null)
    { sprite = (Sprite)wr.Target;
      if(sprite!=null) return sprite;
    }
    sprite = new Sprite(fileName);
    sprites[fileName] = new WeakReference(sprite);
    return sprite;
  }

  internal static void UnloadAll()
  { foreach(WeakReference wr in sprites)
    { Sprite sprite = (Sprite)wr.Target;
      if(sprite!=null) sprite.Dispose();
    }
    sprites.Clear();
  }

  Stream GetStream(string fileName)
  { string path = Engine.SpritePath;
    if(path!=null && File.Exists(path+fileName))
      return File.Open(path+fileName, FileMode.Open, FileAccess.Read);
    else if(Engine.SpriteFile!=null && Engine.SpriteFile.Contains(fileName))
      return Engine.SpriteFile.GetStream(fileName);
    else throw new ArgumentException(string.Format("Unable to locate {0}", fileName));
  }

  void Load(List list) { Load(list["file"].GetString(0), list["elementwidth"].GetInt(0)); }
  void Load(string fileName, int elementWidth) { Load(new Surface(GetStream(fileName)), elementWidth); }
  void Load(Surface surface) { Load(surface, surface.Width); }
  void Load(Surface surface, int elementWidth)
  { texture = new GLTexture2D(surface);
    width = elementWidth;
  }

  void Dispose(bool destructing)
  { if(texture!=null)
    { texture.Dispose();
      texture = null;
    }
  }
  
  GLTexture2D texture;
  int width;
  
  static Hashtable sprites = new Hashtable();
}

} // namespace Bimbo