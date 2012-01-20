﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace Compiler
{
	
	class SymTable
	{
		public class Exception : Compiler.Exception
		{
			public Exception(string s) : base(s) { }
		}

		public SymTable parent = null;
		public List<SymTable> children = new List<SymTable>();
		public int pos = 0, depth = 0;
		protected int count_unnamed_types = 0;


		public Dictionary<string, SymVar> vars = new Dictionary<string, SymVar>();
		public Dictionary<string, SymType> types = new Dictionary<string, SymType>();
		public Dictionary<string, SymVar> consts = new Dictionary<string, SymVar>();

		public SymTable(int pos = 0, int depth = 0)
		{
			this.depth = depth;
			this.pos = pos;
		}

		public string GetUniquePrefix()
		{
			return depth + "_" + pos;
		}

		public void AddConst(SymVar var)
		{
			if (consts.ContainsKey(var.name))
			{
				Symbol.Exception e = new Symbol.Exception("переопределение \"" + var.name + "\"", var.pos, var.line);
				e.Data["delayed"] = true;
				throw e;
			}

			var.pos_in_tables = new Pair<int, int>(this.pos, this.depth);
			consts.Add(var.name, var);
		}

		public bool ContainsConst(string name)
		{
			return consts.ContainsKey(name);
		}

		public SymVar GetConst(string name)
		{
			return consts[name];
		}

		public void AddVar(SymVar var)
		{
			if (vars.ContainsKey(var.GetName()) || consts.ContainsKey(var.GetName()))
			{
				if (var.type is SymTypeFunc && var.Equals(vars[var.name]))
				{
					vars[var.name] = var;
					return;
				}
				Symbol.Exception e = new Symbol.Exception("переопределение \"" + var.GetName() + "\"", var.token.pos, var.token.line);
				e.Data["delayed"] = true;
				throw e;
			}
			var.pos_in_tables = new Pair<int, int>(this.pos, this.depth);
			vars.Add(var.GetName(), var);//qutim
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

			if (type.GetName() == Symbol.UNNAMED)
			{
				type.SetName(Symbol.UNNAMED + count_unnamed_types);
				count_unnamed_types++;
			}

			type.pos_in_tables = new Pair<int, int>(this.pos, this.depth);
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

		public SymVar GetIdentifier(string name)
		{
			return vars[name];
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

			s += "\n" + (is_struct_items ? "const::" : "CONST::") + "\n";
			
			foreach (var x in consts)
			{
				s += x.Key + " " + x.Value.ToString() + "\n\n";
			}

			return s;
		}
	}

	class StackTable
	{
		public class Iterator
		{
			SymTable cur, root;

			public Iterator(StackTable tbs)
			{
				root = tbs.root;
				cur = root;
			}

			public Iterator(SymTable table)
			{
				root = table;
				cur = table;
			}

			public bool MoveUp()
			{
				if (cur.parent == null)
				{
					return false;
				}

				cur = cur.parent;
				return true;
			}

			public bool MoveNext()
			{
				if (cur.children.Count == 0)
				{
					if (cur.parent == null)
					{
						return false;
					}

					SymTable p = cur.parent;

					while (p != null && cur != root)
					{
						for (int i = 0; i < p.children.Count; i++)
						{
							if (p.children[i] == cur && i + 1 < p.children.Count)
							{
								cur = p.children[i + 1];
								return true;
							}
						}
						p = p.parent;
					}
					return false;
				}
				else
				{
					cur = cur.children[0];
					return true;
				}
			}

			public SymTable Current()
			{
				return cur;
			}
		}

		SymTable root, current;

		public StackTable()
		{
			root = new SymTable();
			current = root;
			root.AddType(new SymTypeChar());
			root.AddType(new SymTypeDouble());
			root.AddType(new SymTypeInt());
			root.AddType(new SymTypeVoid());
			
			SymVarGlobal f = new SymVarGlobal();
			f.SetName("printf");
			SymTypeIncludeFunc ft = new SymTypeIncludeFunc(new SymTypeVoid());
			SymVarParam vp = new SymVarParam();
			vp.SetType(new SymTypePointer(new SymTypeChar()));
			ft.SetParam(vp);
			ft.SetUnspecifiedParam();
			f.SetType(ft);
			root.AddVar(f);

			f = new SymVarGlobal();
			f.SetName("scanf");
			ft = new SymTypeIncludeFunc(new SymTypeVoid());
			vp.SetType(new SymTypePointer(new SymTypeChar()));
			ft.SetParam(vp);
			ft.SetUnspecifiedParam();
			f.SetType(ft);
			root.AddVar(f);
		}

		public Iterator Begin()
		{
			return new Iterator(root);
		}

		public void NewTable()
		{
			SymTable new_node = new SymTable(current.children.Count, current.depth + 1);
			new_node.parent = current;
			current.children.Add(new_node);
			current = new_node;
		}

		public SymTable GetCurrent()
		{
			return this.current;
		}

		public void Up()
		{
			if (current.parent == null)
			{
				throw new Exception("Out of range");
			}

			current = current.parent;
		}

		public void AddConst(SymVar c)
		{
			current.AddConst(c);
		}

		public bool ContainsConst(string name)
		{
			Iterator itr = new Iterator(current);
			do
			{
				if (itr.Current().ContainsConst(name))
				{
					return true;
				}
			}
			while (itr.MoveUp());

			return false;
		}

		public SymVar GetConst(string name)
		{
			Iterator itr = new Iterator(current);
			do
			{
				if (itr.Current().ContainsConst(name))
				{
					return itr.Current().GetConst(name);
				}
			}
			while (itr.MoveUp());

			return root.GetConst(name);
		}

		public void AddVar(SymVar var)
		{
			current.AddVar(var);
		}

		public void AddType(SymType t)
		{
			current.AddType(t);
		}

		public SymType GetType(string name)
		{
			Iterator itr = new Iterator(current);
			do 
			{
				if (itr.Current().ContainsType(name))
				{
					return itr.Current().GetType(name);
				}
			} 
			while (itr.MoveUp());

			return null;
		}

		public bool ContainsType(string name)
		{
			Iterator itr = new Iterator(current);
			do
			{
				if (itr.Current().ContainsType(name))
				{
					return true;
				}
			}
			while (itr.MoveUp());

			return false;
		}

		public SymVar GetIdentifier(string name)
		{
			Iterator itr = new Iterator(current);
			do
			{
				if (itr.Current().ContainsIdentifier(name))
				{
					return itr.Current().GetIdentifier(name);
				}
			}
			while (itr.MoveUp());
			return null;
		}

		public bool ContainsIdentifier(string name)
		{
			Iterator itr = new Iterator(current);

			do
			{
				if (itr.Current().ContainsIdentifier(name))
				{
					return true;
				}
			}
			while (itr.MoveUp());

			return false;
		}

		public bool isGlobalTable()
		{
			return root == current;
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
		public class Exception : Compiler.Exception 
		{
			public int pos, line;
			public Exception(string s = "", int pos = -1, int line = -1) : base(s) 
			{
				this.pos = pos;
				this.line = line;
			}
		}

		public const string UNNAMED = "@UNNAMED@";

		public string name;
		public Pair<int, int> pos_in_tables = new Pair<int, int>(-1, -1);

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

	abstract class SymVar : Symbol
	{
		public SymType type = null;
		public SynInit value = null;
		public Token token;
		public int line, pos;

		public SymVar() : base() { }
		public SymVar(Token t)
		{
			token = t;
			this.name = t.strval;
			line = t.line;
			pos = t.pos;
		}

		virtual public void SetType(SymType type)
		{
			this.type = type;
		}

		public void SetInitValue(SynInit val)
		{
			if (!val.getType().Compatible(val.getType()))
			{
				Symbol.Exception e = new Symbol.Exception("значение типа \"" + val.getType().ToString() 
					+ "\" нельзя использовать для инициализации сущности типа \"" + type.ToString() + "\"",
					val.pos, val.line);
				throw e;
			}

			value = val;
		}

		public override string ToString()
		{
			return this.name + "   " + this.type.ToString() + "   " + (this.value == null? "": "\n = " + this.value.ToString());
		}

		public override bool Equals(object obj)
		{
			if (obj is SymVar)
			{
				return this.name == ((SymVar)obj).name && this.type.Equals(((SymVar)obj).type);
			}
			return base.Equals(obj);
		}

		abstract public void GenerateCode(CodeGen.Code code);

		
		public void GenerateInitialize(CodeGen.Code code)
		{
			if (value != null)
			{
				AssignOper init = new AssignOper();
				init.SetLeftOperand(new IdentExpr(this));
				init.SetRightOperand(value);
				init.GenerateCode(code);
			}
		}
	}

	class SymSuperVar : SymVar
	{
		public const string NAME_IN_TABLE = "$SUPER VAR$";

		public SymSuperVar(Token t) : base(t) 
		{
			type = new SymSuperType();
			name = t.strval;
		}

		public SymSuperVar() : base() 
		{
			type = new SymSuperType();
			name = NAME_IN_TABLE;
		}

		public override string ToString()
		{
			return "SUPER VAR" + "   " + this.type.ToString();
		}

		public override bool Equals(object obj)
		{
			if (obj is SymVar)
			{
				return true;
			}

			return base.Equals(obj);
		}

		public override void GenerateCode(CodeGen.Code code)
		{
			throw new NotImplementedException();
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

		public override bool Equals(object obj)
		{
			if (obj is SymVarParam)
			{
				if (((SymVarParam)obj).name == UNNAMED || this.name == UNNAMED || this.name == ((SymVarParam)obj).name)
				{
					return this.type.Equals(((SymVarParam)obj).type);
				}
				return false;
			}

			return base.Equals(obj);
		}

		public string GenerateCode()
		{
			return name + ":" + type.GenerateDeclaratorCode(this);
		}

		public override void GenerateCode(CodeGen.Code code)
		{
			throw new NotImplementedException();
		}
	}

	class SymVarLocal : SymVar
	{
		public SymVarLocal(Token t) : base(t) { }
		public SymVarLocal() : base() { }

		public override void GenerateCode(CodeGen.Code code)
		{
			code.AddLine("LOCAL " + name + ":" + type.GenerateDeclaratorCode(this), 6);
		}
	}

	class SymVarGlobal : SymVar
	{
		public SymVarGlobal(Token t) : base(t) { }
		public SymVarGlobal() : base() { }

		public override void GenerateCode(CodeGen.Code code)
		{
			if (type is SymTypeFunc)
			{
				((SymTypeFunc)type).GenerateDeclarationCode(code);
				return;
			}

			code.AddLine(name + " " + type.GenerateDeclaratorCode(this) + " " + SynInit.GenerateBaseInitCode(type));
		}
	}

	class SymVarConst : SymVar
	{
		public SymVarConst(Token t) : base(t) { }

		public override void GenerateCode(CodeGen.Code code)
		{
			if (type is SymTypeInt)
			{
				code.AddLine(name + " " + type.GenerateDeclaratorCode(this) + " " + value.ComputeConstIntValue());
			}
			else if (type is SymTypeArray)
			{
				code.AddLine(name + " " + type.GenerateDeclaratorCode(this) + " dup(\"" + ((ConstExpr)value.val).value + "\", 13, 10, 0)");
			}
			else
			{
				throw new NotImplementedException();
			}
		}
	}

#endregion

#region Types

	abstract class SymType : Symbol
	{
		virtual public SymTypeScalar GetTailType(){
			return (SymTypeScalar)this;
		}

		public abstract bool Compatible(SymType t);

		public abstract int GetSize();

		public abstract string GenerateDeclaratorCode(SymVar var);

		public abstract void GenerateDeclarationCode(CodeGen.Code code);
	}

	class SymSuperType : SymType
	{
		public SymSuperType() { }

		public override string ToString()
		{
			return "SUPER TYPE";
		}

		public override bool Equals(object obj)
		{
			if (obj is SymType)
			{
				return true;
			}
			return base.Equals(obj);
		}

		public override bool Compatible(SymType t)
		{
			return true;
		}

		override public int GetSize()
		{
			return 0;
		}

		public override void GenerateDeclarationCode(CodeGen.Code code)
		{
			throw new NotImplementedException();
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			throw new NotImplementedException();
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

		override public bool Equals(object obj)
		{
			if (obj is SymTypeScalar)
			{
				return this.name == ((SymTypeScalar)obj).name;
			}
			else if (obj is SymTypeAlias)
			{
				return this.Equals(((SymTypeAlias)obj).type);
			}

			return base.Equals(obj);
		}

		public override void GenerateDeclarationCode(CodeGen.Code code) {  }
	}

	class SymTypeVoid : SymTypeScalar
	{
		public SymTypeVoid(Token t) : base(t) { }
		public SymTypeVoid() : base("void") { }

		public override bool Compatible(SymType t)
		{
			return true;
		}

		public override int GetSize()
		{
			return 0;
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			throw new NotImplementedException();
		}
	}

	class SymTypeDouble : SymTypeScalar
	{
		public SymTypeDouble(Token t) : base(t) { }
		public SymTypeDouble() : base("double") { }

		public override bool Compatible(SymType t)
		{
			return t is SymSuperType || t is SymTypeChar || t is SymTypePointer
				|| t is SymTypeDouble || t is SymTypeEnum || t is SymTypeFunc || t is SymTypeInt;
		}

		public override int GetSize()
		{
			return 8;
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			return "QWORD";
		}
	}

	class SymTypeChar : SymTypeScalar
	{
		public SymTypeChar(Token t) : base(t) { }
		public SymTypeChar() : base("char") { }

		public override bool Compatible(SymType t)
		{
			return t is SymSuperType || t is SymTypeChar || t is SymTypePointer
				|| t is SymTypeDouble || t is SymTypeEnum || t is SymTypeFunc || t is SymTypeInt;
		}

		public override int GetSize()
		{
			return 1;
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			return "BYTE";
		}
	}

	class SymTypeInt : SymTypeScalar
	{
		public SymTypeInt(Token t) : base(t) { }
		public SymTypeInt() : base("int") { }

		public override bool Compatible(SymType t)
		{
			return t is SymSuperType || t is SymTypeChar || t is SymTypePointer 
				|| t is SymTypeDouble || t is SymTypeEnum || t is SymTypeFunc || t is SymTypeInt;
		}

		public override int GetSize()
		{
			return 4;
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			return "DWORD";
		}
	}

	abstract class SymRefType : SymType
	{
		public SymType type;

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

		public override bool Compatible(SymType t)
		{
			return t is SymSuperType || t is SymTypeInt || t is SymTypeChar;
		}

		public override int GetSize()
		{
			return 4;
		}

		public override void GenerateDeclarationCode(CodeGen.Code code)
		{
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			if (var is SymVarLocal)
			{
				return "[" + ((SynConstExpr)size).ComputeConstIntValue() + "]:" + type.GenerateDeclaratorCode(var);
			}
			else
			{
				return type.GenerateDeclaratorCode(var) + " " + ((SynConstExpr)size).ComputeConstIntValue();
			}
		}
	}

	class SymTypeFunc : SymRefType
	{
		public List<SymVarParam> args = new List<SymVarParam>();
		public StmtBLOCK body = null;
		public bool unspecified_number_param = false;

		public SymTypeFunc(SymType t = null)
		{
			this.type = t;
		}

		public void SetParam(SymVarParam p)
		{
			string error = "недопустимо использование типа \"void\"";
			if (args.Count == 1 && args[0].type is SymTypeVoid)
			{
				SymTypeVoid t = (SymTypeVoid)args[0].type;
				throw new Symbol.Exception(error, t.pos, t.line);
			}

			if (p.type is SymTypeVoid)
			{
				SymTypeVoid t = (SymTypeVoid)p.type;
				if (p.GetName() != UNNAMED || args.Count > 0)
				{
					throw new Symbol.Exception(error, t.pos, t.line);
				}
			}

			this.args.Add(p);
		}

		public void SetUnspecifiedParam()
		{
			unspecified_number_param = true;
		}

		public void SetBody(StmtBLOCK _body)
		{
			body = _body;

			if (args.Count == 1 && args[0].type is SymTypeVoid && args[0].GetName() == UNNAMED)
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

				if (arg.type is SymTypeVoid)
				{
					SymTypeVoid t = (SymTypeVoid)arg.type;
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

		override public bool Equals(Object obj)
		{
			if(obj is SymTypeFunc)
			{
				if (name != ((SymTypeFunc)obj).name || !this.type.Equals(((SymTypeFunc)obj).type))
					return false;

				int i = 0, j = 0;
				while (i < this.args.Count && j < ((SymTypeFunc)obj).args.Count)
				{
					while (i < this.args.Count && this.args[i].type is SymTypeVoid) { i++; }
					while (j < ((SymTypeFunc)obj).args.Count && ((SymTypeFunc)obj).args[j].type is SymTypeVoid) { j++; }

					if (!(this.args[i].Equals(((SymTypeFunc)obj).args[j])))
					{
						return false;
					}
					i++; j++;
				}

				return (i == this.args.Count || i == 0) && (j == ((SymTypeFunc)obj).args.Count || j == 0);
			}
			return base.Equals(obj);
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

		public override bool Compatible(SymType t)
		{
			return t is SymSuperType || this.Equals(t);
		}

		public override int GetSize()
		{
			return 4;
		}

		public override void GenerateDeclarationCode(CodeGen.Code code)
		{
			code.SetIndent(3);
			string s = name + " PROC";
			for (int i = 0; i < args.Count; i++)
			{
				args[i].name += "@" + name;
				s += (i == 0 ? " " : ", ") + args[i].GenerateCode();
			}
			code.AddLine(s);


			code.SetIndent(6);
			CodeGen.Code init = new CodeGen.Code(6);
			StackTable.Iterator titr = new StackTable.Iterator(body.table);
			do
			{
				string pr = "@" + titr.Current().depth + "_" + titr.Current().pos;
				foreach (var lv in titr.Current().vars.Values)
				{
					if (!(lv is SymVarParam))
					{
						lv.name += pr;
						lv.GenerateCode(code);
						lv.GenerateInitialize(init);
					}
				}
			} while (titr.MoveNext());
			code = code + init;

			code.SetIndent(6);
			body.GenerateCode(code);

			code.AddLine("RET", 6);
			code.SetIndent(3);
			code.AddLine(name + " ENDP");
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			throw new NotImplementedException();
		}
	}

	class SymTypeIncludeFunc : SymTypeFunc
	{
		public SymTypeIncludeFunc(SymType t) : base(t) { }

		public override void GenerateDeclarationCode(CodeGen.Code code)
		{
		}
	}

	class SymTypeEnum : SymType
	{
		Dictionary<string, SymVar> enumerators = new Dictionary<string, SymVar>();

		public void AddEnumerator(SymVar var)
		{
			if (var.value == null)
			{
				SynInit val = null;
				if(enumerators.Count == 0)
				{
					val = new SynInit(new ConstExpr(new SymTypeInt(), "0"));
				}
				else
				{
					BinaryOper expr = new BinaryOper(new Token(Token.Type.OP_PLUS));
					expr.SetLeftOperand(enumerators.Values.Last().value.val);
					expr.SetRightOperand(new ConstExpr(new SymTypeInt(), "1"));
					val = new SynInit(expr);
				}

				var.SetInitValue(val);
			}

			this.enumerators.Add(var.name, var);
		}

		public override string ToString()
		{
			string s = "ENUM " + this.name + " {\n";
			foreach (var e in this.enumerators)
			{
				s += e.Value.ToString() + '\n';
			}
			s += "}";
			return s;
		}

		public override bool Compatible(SymType t)
		{
			return t is SymSuperType || t is SymTypeInt || t is SymTypeChar || t is SymTypeDouble || t is SymTypeEnum;
		}

		public override int GetSize()
		{
			return 4;
		}

		public override void GenerateDeclarationCode(CodeGen.Code code)
		{
			foreach (SymVarConst c in enumerators.Values)
			{
				c.name += "@" + this.name + this.pos_in_tables.first + "_" + pos_in_tables.last;
				c.GenerateCode(code);
			}
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			return "DWORD";
		}
	}

	class SymTypeStruct : SymType
	{
		public SymTable fields;

		public void SetItems(SymTable table)
		{
			fields = table;
		}

		public override string ToString()
		{
			return  "STRUCT " + this.name + "{" + fields.ToString(true) + "}";
		}

		public override bool Compatible(SymType t)
		{
			return this.Equals(t);
		}

		public override int GetSize()
		{
			int size = 0;
			foreach (var field in fields.vars)
			{
				size += field.Value.type.GetSize();
			}

			return size;
		}

		public override void GenerateDeclarationCode(CodeGen.Code code)
		{
			this.name += "@" + pos_in_tables.first + "_" + pos_in_tables.last;
			code.AddLine(this.name + " STRUCT", 3);
			code.SetIndent(6);
			foreach (SymVar v in fields.vars.Values)
			{
				v.GenerateCode(code);
			}
			code.SetIndent(3);
			code.AddLine(this.name + " ENDS");	
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			return this.name;
		}
	}

	class SymTypeAlias : SymRefType
	{
		protected int line = -1, pos = -1;
		public SymTypeAlias(SymVar var)
		{
			this.type = var.type;
			this.name = var.GetName();
			this.line = var.token.line;
			this.pos = var.token.pos;
		}

		public override string ToString()
		{
			return type.ToString();
		}

		public override SymTypeScalar GetTailType()
		{
			return type.GetTailType();
		}

		public override bool Equals(object obj)
		{
			if (obj is SymTypeAlias)
			{
				return this.name == ((SymTypeAlias)obj).name && this.type.Equals(((SymTypeAlias)obj).type);
			}

			return base.Equals(obj);
		}

		public override bool Compatible(SymType t)
		{
			return this.type.Compatible(t);
		}

		public override int GetSize()
		{
			return type.GetSize();
		}

		public override void GenerateDeclarationCode(CodeGen.Code code)
		{
			throw new NotImplementedException();
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			throw new NotImplementedException();
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

		public override bool Compatible(SymType t)
		{
			return t is SymSuperType || t is SymTypeInt || t is SymTypeChar || t is SymTypePointer 
				|| t is SymTypeArray || t is SymTypeFunc || t is SymTypeEnum || t is SymTypeFunc;
		}

		public override int GetSize()
		{
			return type.GetSize();
		}

		public override void GenerateDeclarationCode(CodeGen.Code code)
		{
			throw new NotImplementedException();
		}

		public override string GenerateDeclaratorCode(SymVar var)
		{
			return "DWORD";
		}
	}

#endregion
}
