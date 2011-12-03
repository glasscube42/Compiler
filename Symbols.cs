﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace Compiler
{
	
	class SymTable
	{
		public class Exception : System.Exception 
		{
			public Exception(string msg) : base(msg) { }
		}

		public SymTable parent = null;
		Dictionary<string, SymVar> vars = new Dictionary<string, SymVar>();
		Dictionary<string, SymType> types = new Dictionary<string, SymType>();

		public void AddVar(SymVar var)
		{
			if (vars.ContainsKey(var.GetName()))
			{
				if((Symbol)var is SymTypeFunc && ((SymTypeFunc)(Symbol)var).Equals((SymTypeFunc)(Symbol)vars[var.GetName()]))
				throw new Symbol.Exception("переопределение \"" + var.GetName() + "\"", var.token.pos, var.token.line);
			}

			vars.Add(var.GetName(), var);//qutim
		}

		public void AddDummyVar(string name)
		{
			vars.Add(name, null);
		}

		public void AddType(SymType type)
		{
			if (type == null)
			{
				throw new SymTable.Exception("отсутствует спецификатор типа");
			}

			if (type.GetName() != Symbol.UNNAMED && types.ContainsKey(type.GetName()))
			{
				throw new SymTable.Exception("переопределение типа \"" + type.GetName() + "\"");
			}
			
			types.Add(type.GetName(), type);
		}

		public SymType GetType(string name)
		{
			if (!types.ContainsKey(name))
			{
				throw new SymTable.Exception("тип \"" + name + "\" неопределен");
			}

			return types[name];
		}

		public bool ContainsIdentifier(string name)
		{
			return vars.ContainsKey(name);
		}

		public bool ContainsType(string name)
		{
			return types.ContainsKey(name);
		}

		public string ToString(bool is_struct_items = false)
		{
			string s = (is_struct_items? "\nvars::\n": "<---TABLE--->\nVARS::\n");

			foreach (var x in vars)
			{
				s += '\n';
				if (x.Value == null)
				{
					s += x.Key + " $DUMMY$\n\n"; 
				}
				else
				{
					s += x.Value.ToString() + "\n\n";
				}
			}

			s += "\n" + (is_struct_items? "types::": "TYPES::") +"\n";

			foreach (var x in types)
			{
				s += x.Key + " " + x.Value.ToString() + "\n\n";
			}

			return s;
		}
	}

	class StackTable
	{
		class Node : SymTable
		{
			public class Iterator{
				Node cur, root;

				public Iterator(Node node)
				{
					root = node;
					cur = root;
				}

				public bool MoveNext()
				{
					if (cur.children.Count == 0)
					{
						if (cur.parent == null)
						{
							return false;
						}

						Node p = (Node)cur.parent;

						while (p != null)
						{
							for (int i = 0; i < p.children.Count; i++)
							{
								if (p.children[i] == cur && i + 1 < p.children.Count)
								{
									cur = p.children[i + 1];
									return true;
								}
							}
							p = (Node)p.parent;
						}
						return false;
					}
					else
					{
						cur = cur.children[0];
						return true;
					}
				}

				public Node Current()
				{
					return cur;
				}
			}

			public List<Node> children = new List<Node>();

			new public string ToString()
			{
				string s = base.ToString();
				foreach (var t in children)
				{
					s += t.ToString();
				}

				return s;
			}
		}

		public int depth = 0;

		Node root, current;

		public StackTable()
		{
			root = new Node();
			current = root;
			root.AddType(new SymTypeChar());
			root.AddType(new SymTypeDouble());
			root.AddType(new SymTypeInt());
			root.AddType(new SymTypeVoid());
		}

		public void NewTable()
		{
			Node new_node = new Node();
			new_node.parent = current;
			current.children.Add(new_node);
			current = new_node;
			depth++;
		}

		public void Up()
		{
			if (current.parent == null)
			{
				throw new Exception("Out of range");
			}

			current = (Node)current.parent;
			depth--;
		}

		public void AddVar(SymVar var)
		{
			current.AddVar(var);
		}

		public void AddDummyVar(string name)
		{
			current.AddDummyVar(name);
		}

		public void AddType(SymType t)
		{
			current.AddType(t);
		}

		public SymType GetType(string name)
		{
			Node.Iterator itr = new Node.Iterator(root);
			do 
			{
				if (itr.Current().ContainsType(name))
				{
					return itr.Current().GetType(name);
				}
			} 
			while (itr.MoveNext());

			return root.GetType(name);
		}

		public bool ContainsType(string name)
		{
			try
			{
				GetType(name);
				return true;
			}
			catch (SymTable.Exception)
			{
				return false;
			}
		}


		public bool ContainsIdentifier(string name)
		{
			Node.Iterator itr = new Node.Iterator(root);

			do
			{
				if (itr.Current().ContainsIdentifier(name))
				{
					return true;
				}
			}
			while (itr.MoveNext());

			return false;
		}

		public override string ToString()
		{
			string s = "<--------TABLES-------->\n";
			s += root.ToString();

			return s;
		}
	}

	class Symbol
	{
		public class Exception : System.Exception 
		{
			public int pos, line;
			public Exception(string s = "", int pos = -1, int line = -1) : base(s) 
			{
				this.pos = pos;
				this.line = line;
			}
		}

		public const string UNNAMED = "$UNNAMED$";

		protected string name;

		public Symbol(string name)
		{
			this.name = name;
		}

		public Symbol()
		{
			this.name = UNNAMED;
		}

		public void SetName(string s)
		{
			name = s;
		}

		public string GetName()
		{
			return name;
		}

		public override string ToString()
		{
			return this.name;
		}
	}

#region Variable

	class SymVar : Symbol
	{
		protected SymType type = null;
		protected SynExpr value = null;
		public Token token;
		public int line, pos;

		public SymVar() : base() { }
		public SymVar(Token t)
		{
			token = t;
			this.name = t.strval;
		}

		virtual public void SetType(SymType type)
		{
			this.type = type;
		}

		public void SetInitValue(SynInit val)
		{
			value = val;
		}

		new public SymType GetType()
		{
			return this.type;
		}

		public override string ToString()
		{
			return this.name + "   " + this.type.ToString() + "   " + (this.value == null? "": "\n = " + this.value.ToString());
		}
	}

	class SymVarParam : SymVar
	{
		public SymVarParam(Token t) : base(t) 
		{
			line = t.line;
			pos = t.pos;
		}
		public SymVarParam() : base() { }
		public SymVarParam(int line, int pos)
		{
			this.line = line;
			this.pos = pos;
		}
	}

	class SymVarLocal : SymVar
	{
	}

	class SymVarGlobal : SymVar
	{

	}

#endregion

#region Types

	abstract class SymType : Symbol
	{
		virtual public SymTypeScalar GetTailType(){
			return (SymTypeScalar)this;
		}
	}

	abstract class SymTypeScalar : SymType
	{
		public int line, pos;
		public SymTypeScalar(string s)
		{
			this.name = s;
		}

		public SymTypeScalar(Token t)
		{
			name = t.strval;
			line = t.line;
			pos = t.pos;
		}
	}

	class SymTypeVoid : SymTypeScalar
	{
		public SymTypeVoid(Token t) : base(t) { }
		public SymTypeVoid() : base("void") { }
	}

	class SymTypeDouble : SymTypeScalar
	{
		public SymTypeDouble(Token t) : base(t) { }
		public SymTypeDouble() : base("double") { }
	}

	class SymTypeChar : SymTypeScalar
	{
		public SymTypeChar(Token t) : base(t) { }
		public SymTypeChar() : base("char") { }
	}

	class SymTypeInt : SymTypeScalar
	{
		public SymTypeInt(Token t) : base(t) { }
		public SymTypeInt() : base("int") { }
	}

	abstract class SymRefType : SymType
	{
		protected SymType type;

		public SymRefType(SymType t = null)
		{
			type = null;
		}

		public void SetType(SymType t)
		{
			this.type = t;
		}

		public override SymTypeScalar GetTailType()
		{
			SymType itr = this.type;
			while (itr is SymRefType)
			{
				itr = ((SymRefType)itr).type;
			}

			return (SymTypeScalar)itr;
		}
	}

	class SymTypeArray : SymRefType
	{
		SynExpr size = null;
		public SymTypeArray(SymType t = null)
		{
			this.type = t;
		}

		public void SetSize(SynExpr size)
		{
			this.size = size;
		}

		public override string ToString()
		{
			return "ARRAY (" + (size == null? "" :size.ToString()) + ") OF " + type.ToString(); 
		}
	}

	class SymTypeFunc : SymRefType
	{
		public List<SymVarParam> args = new List<SymVarParam>();
		public SynStmt body = null;

		public SymTypeFunc(SymType t = null)
		{
			this.type = t;
		}

		public void SetParam(SymVarParam p)
		{
			string error = "недопустимо использование типа \"void\"";
			if (args.Count == 1 && args[0].GetType() is SymTypeVoid)
			{
				SymTypeVoid t = (SymTypeVoid)args[0].GetType();
				throw new Symbol.Exception(error, t.pos, t.line);
			}

			if (p.GetType() is SymTypeVoid)
			{
				SymTypeVoid t = (SymTypeVoid)p.GetType();
				if (p.GetName() != UNNAMED || args.Count > 0)
				{
					throw new Symbol.Exception(error, t.pos, t.line);
				}
			}

			this.args.Add(p);
		}

		public void SetBody(SynStmt _body)
		{
			body = _body;

			if (args.Count == 1 && args[0].GetType() is SymTypeVoid && args[0].GetName() == UNNAMED)
			{
				return;
			}

			foreach (var arg in args)
			{
				if (arg.GetName() == UNNAMED)
				{
					Symbol.Exception e = new Symbol.Exception("требуется идентификатор", arg.pos, arg.line);
					e.Data["delayed"] = true;
					throw e;
				}

				if (arg.GetType() is SymTypeVoid)
				{
					SymTypeVoid t = (SymTypeVoid)arg.GetType();
					Symbol.Exception e = new Symbol.Exception("недопустимо использование типа \"void\"", t.pos, t.line);
					e.Data["delayed"] = true;
					throw e;
				}
			}
		}

		public bool IsEmptyBody()
		{
			return body == null;
		}

		public bool Equals(SymTypeFunc obj)
		{
			if (name != obj.name || this.type != obj.type)
				return false;

			foreach(var arg1 in args){
				foreach (var arg2 in ((SymTypeFunc)obj).args)
				{
					if (arg2 != arg1)
					{
						return false;
					}
				}
			}

			return true;
		}

		public override string ToString()
		{
			string s = "FUNC (";

			for (int i = 0; i < args.Count; i++)
			{
				s += args[i].ToString() + (i == args.Count - 1? "": ",");
			}

			s += (body == null ? ") " : ") { " + body.ToString() + " }") + " RETURNED " + this.type.ToString();
			return s;
		}
	}

	class SymTypeEnum : SymType
	{
		Dictionary<string, SynExpr> enumerators = new Dictionary<string, SynExpr>();

		public void AddEnumerator(string name, SynExpr val)
		{
			this.enumerators.Add(name, val);
		}

		public void AddEnumerator(string name)
		{
			this.enumerators.Add(name, null);
		}

		public override string ToString()
		{
			string s = "ENUM " + this.name + " {\n";
			foreach (var e in this.enumerators)
			{
				s += e.Key + (e.Value == null ? "" : "=" + e.Value.ToString()) + '\n';
			}
			s += "}";
			return s;
		}
	}

	class SymTypeStruct : SymType
	{
		SymTable fields;

		public void SetItems(SymTable table)
		{
			fields = table;
		}

		public override string ToString()
		{
			return  "STRUCT " + this.name + "{" + fields.ToString(true) + "}";
		}
	}

	class SymTypeAlias : SymType
	{
		public int line, pos;
		SymType type;
		public SymTypeAlias(SymVar var)
		{
			this.type = var.GetType();
			this.name = var.GetName();
			this.line = var.token.line;
			this.pos = var.token.pos;
		}

		public override string ToString()
		{
			return type.ToString();
		}
	}

	class SymTypePointer : SymRefType
	{
		public SymTypePointer(SymType t = null)
		{
			this.type = t;
		}

		public override string ToString()
		{
			return "POINTER TO " + type.ToString();
		}
	}

#endregion
}
