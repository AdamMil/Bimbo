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
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;

namespace Bimbo
{

#region Animation
public enum LoopType { None, Forward, Reverse, PingPong };

public class Animation
{ public Animation() { }
  public Animation(LoopType looping) { loop=looping; }
  public Animation(Chunk[] chunks) { if(chunks.Length!=0) this.chunks = (Chunk[])chunks.Clone(); length=chunks.Length; }
  public Animation(Chunk[] chunks, LoopType looping) : this(chunks) { loop=looping; }
  public Animation(Chunk chunk) { AddChunk(chunk); }
  public Animation(Chunk chunk, LoopType looping) { AddChunk(chunk); loop = looping; }

  public struct Chunk
  { public Chunk(int frame, float delay) { Start=frame; Length=1; Delay=delay; }
    public Chunk(int start, int length, float delay) { Start=start; Length=length; Delay=delay; }
    public int Start, Length;
    public float Delay;
  }
  
  public struct Position
  { public void Reset() { Chunk=Index=0; Done=Reversing=false; }

    public int Chunk;
    public int Index;
    public float Time;
    public bool Done, Reversing;
  }

  public Chunk[] Chunks { get { return chunks; } }
  public int Length { get { return length; } }
  public LoopType Looping { get { return loop; } set { loop=value; } }

  public void AddChunk(Chunk chunk)
  { if(chunks==null) chunks = new Chunk[1];
    else if(length==chunks.Length)
    { Chunk[] narr = new Chunk[length*2];
      Array.Copy(chunks, narr, length);
      chunks = narr;
    }
    chunks[length++] = chunk;
  }

  public int GetFrame(Position pos)
  { return (chunks[pos.Chunk].Length<0 ? -pos.Index : pos.Index) + chunks[pos.Chunk].Start;
  }

  public Position Update(Position pos, float timeDelta)
  { if(length==0) return pos;
    pos.Time += timeDelta;
    while(pos.Time>=chunks[pos.Chunk].Delay)
    { pos.Time -= chunks[pos.Chunk].Delay;
      int clen = Math.Abs(chunks[pos.Chunk].Length);
      pos.Index += loop==LoopType.Reverse || pos.Reversing ? -1 : 1;
      if(pos.Index>=clen || pos.Index<0)
        switch(loop)
        { case LoopType.None: pos.Done=true; return pos;
          case LoopType.Forward: if(++pos.Chunk>=length) pos.Chunk=0; pos.Index=0; break;
          case LoopType.Reverse: if(--pos.Chunk<0) pos.Chunk=length-1; pos.Index=clen-1; break;
          case LoopType.PingPong:
            if(clen<2) pos.Index=0;
            else if(pos.Reversing)
            { if(--pos.Chunk<0) { pos.Chunk=0; pos.Index = 1; pos.Reversing = false; }
              else pos.Index = clen-1;
            }
            else
            { if(++pos.Chunk>=length) { pos.Chunk = length-1; pos.Index = clen-2; pos.Reversing = true; }
              else pos.Index = 0;
            }
            break;
        }
    }
    return pos;
  }

  Chunk[] chunks;
  int length;
  LoopType loop = LoopType.Forward;
}
#endregion

#region Sprite
public class Sprite
{ public Sprite(string fileName)
  { if(fileName.ToLower().EndsWith(".sps")) Load(new List(GetSpriteStream(fileName)));
    else Load(new Surface(GetSpriteStream(fileName), ImageType.PNG)); // FIXME: assumes PNG!
  }
  public Sprite(List list) { Load(list); }

  public Sprite(string fileName, int elementWidth) { Load(fileName, elementWidth); }
  public Sprite(Surface surface) { Load(surface, surface.Width); }
  public Sprite(Surface surface, int elementWidth) { Load(surface, elementWidth); }

  public int Height { get { return texture.ImgHeight; } }
  public int Width { get { return width; } }

  public void Render(Point center, int frame)
  { if(frame<0 || frame>=frames) throw new ArgumentOutOfRangeException("frame");
    float sx = center.X-width*0.5f, sy = center.Y-(float)texture.ImgHeight*0.5f;
    double tx = frame*width/(double)texture.TexWidth, tx2 = width/(double)texture.TexWidth + tx;
    double th = texture.ImgHeight/(double)texture.TexHeight;
    GL.glColor(System.Drawing.Color.White);
    texture.Bind();
    GL.glBegin(GL.GL_QUADS);
      GL.glTexCoord2d(tx,  0);  GL.glVertex2f(sx,       sy);
      GL.glTexCoord2d(tx2, 0);  GL.glVertex2f(sx+width, sy);
      GL.glTexCoord2d(tx2, th); GL.glVertex2f(sx+width, sy+texture.ImgHeight);
      GL.glTexCoord2d(tx,  th); GL.glVertex2f(sx,       sy+texture.ImgHeight);
    GL.glEnd();
  }

  public static Sprite GetIfLoaded(string fileName)
  { WeakReference wr = (WeakReference)sprites[fileName];
    Sprite sprite;
    if(wr!=null)
    { sprite = (Sprite)wr.Target;
      if(sprite!=null) return sprite;
    }
    return null;
  }

  public static Stream GetSpriteStream(string fileName)
  { string path = Engine.SpritePath;
    if(path!=null && File.Exists(path+fileName))
      return File.Open(path+fileName, FileMode.Open);
    else if(Engine.SpriteFile!=null && Engine.SpriteFile.Contains(fileName))
      return Engine.SpriteFile.GetStream(fileName);
    else throw new ArgumentException(string.Format("Unable to locate {0}", fileName));
  }

  public static Sprite Load(string fileName)
  { Sprite sprite = GetIfLoaded(fileName);
    if(sprite==null)
    { sprite = new Sprite(fileName);
      sprites[fileName] = new WeakReference(sprite);
    }
    return sprite;
  }

  public static void UnloadAll()
  { foreach(WeakReference wr in sprites.Values)
    { Sprite sprite = (Sprite)wr.Target;
      if(sprite!=null) sprite.texture.Dispose();
    }
    sprites.Clear();
  }
  
  void Load(List list) { Load(list["file"].GetString(0), list["elementwidth"].GetInt(0)); }
  void Load(string fileName, int elementWidth)
  { Load(new Surface(GetSpriteStream(fileName), ImageType.PNG), elementWidth); // FIXME: assumes png format!
  }
  void Load(Surface surface) { Load(surface, surface.Width); }
  void Load(Surface surface, int elementWidth)
  { texture = new GLTexture2D(surface);
    texture.Bind();
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_NEAREST);
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_NEAREST);
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, GL.GL_CLAMP_TO_EDGE);
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, GL.GL_CLAMP_TO_EDGE);
    width = elementWidth; frames = surface.Width/width;
  }

  GLTexture2D texture;
  int width, frames;
  
  static Hashtable sprites = new Hashtable();
}
#endregion

} // namespace Bimbo