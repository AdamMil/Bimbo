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
using System.IO;

namespace Bimbo
{

public class List : IEnumerable
{ public List() { name=string.Empty; }
  public List(string name) { this.name=name; }
  public List(string name, params object[] items) { this.name=name; foreach(object o in items) Add(o); }
  public List(Stream stream) { name=string.Empty; Read(new StreamReader(stream)); }

  public object this[int index] { get { return items[index]; } set { items[index]=value; } }

  public List this[string name]
  { get
    { foreach(object o in items) if(o is List) { List list=(List)o; if(list.Name==name) return list; }
      return null;
    }
  }

  public bool Contains(string name)
  { foreach(object o in items) if(o is List) { List list=(List)o; if(list.Name==name) return true; }
    return false;
  }

  public int Length { get { return items.Count; } }

  public string Name
  { get { return name; }
    set { if(value==null) throw new ArgumentNullException("Name"); name=value; }
  }

  public void Add(object o) { if(o==null) throw new ArgumentNullException(); items.Add(o); }

  public void Clear() { items.Clear(); }
  
  public double GetFloat(int index) { return (double)items[index]; }
  public int GetInt(int index) { return (int)(double)items[index]; }
  public List GetList(int index) { return (List)items[index]; }
  public string GetString(int index) { return (string)items[index]; }

  public void Load(Stream stream)
  { Clear();
    Read(new StreamReader(stream));
  }

  public void Save(Stream stream)
  { StreamWriter sw = new StreamWriter(stream);
    Write(sw, 0);
    sw.Flush();
  }
  
  public System.Drawing.Color ToColor()
  { if(items.Count==1)
    { string[] s = GetString(0).Split(',');
      return s.Length==3 ? System.Drawing.Color.FromArgb(byte.Parse(s[0]), byte.Parse(s[1]), byte.Parse(s[2]))
              : System.Drawing.Color.FromArgb(byte.Parse(s[3]), byte.Parse(s[0]), byte.Parse(s[1]), byte.Parse(s[2]));
    }
    else if(items.Count==3) return System.Drawing.Color.FromArgb(GetInt(0), GetInt(1), GetInt(2));
    else return System.Drawing.Color.FromArgb(GetInt(3), GetInt(0), GetInt(1), GetInt(2));
  }

  public System.Drawing.Point ToPoint() { return new System.Drawing.Point(GetInt(0), GetInt(1)); }

  public override string ToString()
  { StringWriter s = new StringWriter();
    Write(s, 0);
    return s.ToString();
  }

  public IEnumerator GetEnumerator() { return items.GetEnumerator(); }

  public static object ListToObject(List list, Type destType)
  { if(destType==typeof(System.Drawing.Point)) return list.ToPoint();
    if(destType==typeof(GameLib.Mathematics.TwoD.Point))
      return new GameLib.Mathematics.TwoD.Point((float)list.GetFloat(0), (float)list.GetFloat(1));
    if(destType==typeof(GameLib.Mathematics.TwoD.Vector))
      return new GameLib.Mathematics.TwoD.Vector((float)list.GetFloat(0), (float)list.GetFloat(1));
    if(destType==typeof(System.Drawing.Color)) return list.ToColor();
    return Convert.ChangeType(list[0], destType);
  }

  public static string ObjectToString(object o) { return ObjectToString(o, false); }
  public static string ObjectToString(object o, bool preferList)
  { Type type = o.GetType();
    if(type==typeof(string))
    { string s = (string)o;
      char delim = s.IndexOf('\"')==-1 ? '\"' : '\'';
      return delim + s + delim;
    }
    if(type==typeof(double)) return ((double)o).ToString("R");
    if(type==typeof(float)) return ((float)o).ToString("R");
    if(type==typeof(GameLib.Mathematics.TwoD.Point))
    { GameLib.Mathematics.TwoD.Point pt = (GameLib.Mathematics.TwoD.Point)o;
      return string.Format(preferList ? "({0:R} {1:R})" : "{0:R} {1:R}", pt.X, pt.Y);
    }
    if(type==typeof(GameLib.Mathematics.TwoD.Vector))
    { GameLib.Mathematics.TwoD.Vector vect = (GameLib.Mathematics.TwoD.Vector)o;
      return string.Format(preferList ? "({0:R} {1:R})" : "{0:R} {1:R}", vect.X, vect.Y);
    }
    if(type==typeof(System.Drawing.Color))
    { System.Drawing.Color c = (System.Drawing.Color)o;
      return string.Format(preferList ? "({0} {1} {2} {3})" : "{0} {1} {2} {3}", c.R, c.G, c.B, c.A);
    }
    if(type==typeof(System.Drawing.Point))
    { System.Drawing.Point pt = (System.Drawing.Point)o;
      return string.Format(preferList ? "({0} {1})" : "{0} {1}", pt.X, pt.Y);
    }
    return o.ToString();
  }

  #region Internals
  void Read(TextReader stream) // .NET needs higher-level text IO
  { int read = SkipWS(stream);
    if(read!='(')
      throw new ArgumentException(string.Format("Expected '(' [got {0}] near {1}", read, stream.ReadLine()));

    stream.Read();
    read=SkipWS(stream); // skip to name

    // read name
    if(char.IsLetter((char)read))
      do
      { name += (char)read;
        stream.Read();
      } while((read=stream.Peek())!=-1 && !char.IsWhiteSpace((char)read) && read!='(' && read!=')');
    if(read==-1) throw new EndOfStreamException();
    
    while(true)
    { read = SkipWS(stream);
      if(read=='(')
      { List list = new List();
        list.Read(stream);
        Add(list);
      }
      else if(read=='-' || char.IsDigit((char)read))
      { string value=string.Empty;
        while(true)
        { read=stream.Peek();
          if(read==-1) throw new EndOfStreamException();
          if(read!='-' && read!='.' && !char.IsDigit((char)read)) break;
          value += (char)stream.Read();
        }
        Add(double.Parse(value));
      }
      else if(read=='\"' || read=='\'')
      { stream.Read();
        string value=string.Empty;
        int delim=read;
        while(true)
        { read=stream.Read();
          if(read==-1) throw new EndOfStreamException("Unterminated string");
          if(read==delim) break;
          value += (char)read;
        }
        Add(value);
      }
      else if(read==')') { stream.Read(); return; }
      else throw new ArgumentException(string.Format("Unknown character '{0}'", (char)read));
    }
  }

  void Write(TextWriter stream, int level)
  { stream.Write('(');
    bool wrote = name!="";
    if(wrote) stream.Write(name);
    for(int i=0; i<items.Count; i++)
    { if(wrote) stream.Write(' ');
      object o = items[i];
      if(o is List)
      { if(level==0) stream.Write("\n  ");
        ((List)o).Write(stream, level+1);
      }
      else stream.WriteLine(ObjectToString(o));
      wrote=true;
    }
    stream.Write(')');
  }
  
  int SkipWS(TextReader stream)
  { int read;
    while(char.IsWhiteSpace((char)(read=stream.Peek()))) stream.Read();
    if(read==-1) throw new EndOfStreamException();
    return read;
  }
  #endregion

  string    name;
  ArrayList items = new ArrayList(2);
}

} // namespace Bimbo