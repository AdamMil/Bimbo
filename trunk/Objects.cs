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
using System.Reflection;
using GameLib.Mathematics.TwoD;

namespace Bimbo
{

[AttributeUsage(AttributeTargets.Field)]
public class SerializableAttribute : Attribute
{ public virtual object Deserialize(List list, Type destType) { return List.ListToObject(list, destType); }
  public virtual string Serialize(object obj) { return List.ObjectToString(obj); }

  public string Name;
}

public abstract class BimboObject
{ public BimboObject() { }
  public BimboObject(List list)
  { foreach(FieldInfo f in GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance))
    { object[] alist = f.GetCustomAttributes(typeof(SerializableAttribute), true);
      if(alist.Length==0) continue;
      SerializableAttribute attr = (SerializableAttribute)alist[0];
      List prop = list[attr.Name==null ? f.Name : attr.Name];
      if(prop!=null) f.SetValue(this, attr.Deserialize(prop, f.FieldType));
    }
    
    PartCoords = World.WorldToPart(pos);
  }

  public int Layer { get { return layer; } set { layer=value; } }

  public Point Pos { get { return pos; } }
  public Vector Vel { get { return vel; } }

  public abstract void Render();
  public abstract void Update();

  public void Serialize(System.IO.TextWriter writer)
  { Type mytype = GetType();
    writer.Write('('+mytype.Name);

    foreach(FieldInfo f in mytype.GetFields(BindingFlags.NonPublic|BindingFlags.Instance))
    { object[] alist = f.GetCustomAttributes(typeof(SerializableAttribute), true);
      if(alist.Length==0) continue;
      SerializableAttribute attr = (SerializableAttribute)alist[0];
      object val = f.GetValue(this);
      if(val!=null) writer.Write(" ({0} {1})", attr.Name==null ? f.Name : attr.Name, attr.Serialize(val));
    }
    writer.WriteLine(')');
  }

  public static BimboObject CreateObject(World world, List list)
  { string name = list.Name.IndexOf('.')==-1 ? Engine.DefaultObjectNamespace+'.'+list.Name : list.Name;
    Type type = Engine.ObjectAssembly.GetType(name);
    if(type==null) throw new ArgumentException("No such object type: "+name);
    ConstructorInfo cons = type.GetConstructor(new Type[] { typeof(List) });
    if(cons==null)
      throw new ArgumentException(string.Format("The object '{0}' does not implement a deserializing constructor.",
                                                name));
    BimboObject obj = (BimboObject)cons.Invoke(new object[] { list });
    obj.world = world;
    return obj;
  }

  internal System.Drawing.Point PartCoords;
  protected World world;

  [Serializable] protected string name;
  [Serializable] protected Point pos;
  [Serializable] protected Vector vel;

  int layer;
}

} // namespace Bimbo