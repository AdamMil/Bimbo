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

using Bimbo;
using Bimbo.Objects;

namespace BimboApp
{

public class BimboAppWorld : Bimbo.World
{ public BimboAppWorld() { }
  public BimboAppWorld(string path) : base(path) { }

  protected override void OnLoad()
  { if(writing) demo = System.IO.File.Open("c:/demo", System.IO.FileMode.OpenOrCreate);
    else demo = System.IO.File.Open("c:/demo", System.IO.FileMode.Open, System.IO.FileAccess.Read);
  }

  protected override void OnUnload()
  { if(demo!=null) demo.Close();
  }

  public override void Update(float timeDelta)
  { if(writing)
    { GameLib.IO.IOH.WriteFloat(demo, timeDelta);
      demo.WriteByte(GameLib.Input.Keyboard.Pressed(GameLib.Input.Key.Left) ? (byte)1 : (byte)0);
      demo.WriteByte(GameLib.Input.Keyboard.Pressed(GameLib.Input.Key.Right) ? (byte)1 : (byte)0);
      demo.WriteByte(GameLib.Input.Keyboard.Pressed(GameLib.Input.Key.Up) ? (byte)1 : (byte)0);
    }
    else
    { if(demo.Position==demo.Length) return;
      timeDelta = GameLib.IO.IOH.ReadFloat(demo);
      GameLib.Input.Keyboard.Press(GameLib.Input.Key.Left, demo.ReadByte()!=0);
      GameLib.Input.Keyboard.Press(GameLib.Input.Key.Right, demo.ReadByte()!=0);
      GameLib.Input.Keyboard.Press(GameLib.Input.Key.Up, demo.ReadByte()!=0);
      int stop=114;
      if(frame==stop) frame=stop;
    }
    try { base.Update(timeDelta); }
    catch(System.Exception e) { demo.Close(); throw e; }
    Coin.AnimPos = Coin.Anim.Update(Coin.AnimPos, timeDelta);
    frame++;
  }
  
  System.IO.Stream demo;
  int frame;
  bool writing=true;
}

} // namespace BimboApp