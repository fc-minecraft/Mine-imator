using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CppGen
{
    // CppGen console application.
    class Program
    {
        public static Dictionary<string, Object> Objects = new Dictionary<string, Object>();
        public static Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        public static Dictionary<string, Shader> Shaders = new Dictionary<string, Shader>();
        public static Dictionary<string, Function> Functions = new Dictionary<string, Function>();
        public static Dictionary<string, ExternalFunction> ExternalFunctions = new Dictionary<string, ExternalFunction>();
        public static Dictionary<string, Variable> GlobalVars = new Dictionary<string, Variable>();
        public static Dictionary<string, Variable> UnknownScopeVars = new Dictionary<string, Variable>();
        public static Dictionary<string, MacroStatement> Macros = new Dictionary<string, MacroStatement>();
        public static Dictionary<string, EnumStatement> Enums = new Dictionary<string, EnumStatement>();
        public static List<string> Strings = new List<string>();
        public static Function AppStepFunction = null;
        public static Function AppDrawFunction = null;
        public static Function AppHttpFunction = null;
        public static Function AppGameEndFunction = null;
        public static List<string> SyntaxErrors = new List<string>();
        public static bool MergeUnknownVars = false;

        public static Dictionary<string, DataType> VarTypeOverride = new Dictionary<string, DataType>()
        {
            ["build_pos"] = new DataType(DataType.Type.Integer),
            ["build_pos_x"] = new DataType(DataType.Type.Integer),
            ["build_pos_y"] = new DataType(DataType.Type.Integer),
            ["build_pos_z"] = new DataType(DataType.Type.Integer),
            ["build_size_x"] = new DataType(DataType.Type.Integer),
            ["build_size_y"] = new DataType(DataType.Type.Integer),
            ["build_size_z"] = new DataType(DataType.Type.Integer),
            ["build_size_xy"] = new DataType(DataType.Type.Integer),
            ["build_size_total"] = new DataType(DataType.Type.Integer),
            ["build_single_stateid"] = new DataType(DataType.Type.Integer),
            ["block_pos_x"] = new DataType(DataType.Type.Integer),
            ["block_pos_y"] = new DataType(DataType.Type.Integer),
            ["block_pos_z"] = new DataType(DataType.Type.Integer),
            ["blockstartpos"] = new DataType(DataType.Type.Integer),
            ["blockendpos"] = new DataType(DataType.Type.Integer)
        };

        static void Main(string[] args)
        {
            // ФИКС: Используем путь к папке, где лежит сам EXE/DLL, а не рабочую папку
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Логика по умолчанию (если аргументы не переданы)
            string workingDir = Directory.GetCurrentDirectory();
            string parentDir = new DirectoryInfo(workingDir).Parent.FullName;
            
            // ФИКС: Используем Path.Combine для кроссплатформенности вместо "\"
            string gmDir = parentDir;
            string outputCodeDir = Path.Combine(parentDir, "CppProject", "Generated");
            string outputSpritesDir = Path.Combine(parentDir, "CppProject", "Asset", "Sprites");
            string outputShadersDir = Path.Combine(parentDir, "CppProject", "Asset", "Shaders");
            string jsonFile = Path.Combine(exeDir, "gml.json"); // Ищем json рядом с exe
            bool genGmlFunc = true;

            if (args.Length == 5)
            {
                gmDir = args[0];
                outputCodeDir = args[1];
                outputSpritesDir = args[2];
                outputShadersDir = args[3];
                // Если путь к json передан, используем его, иначе дефолтный рядом с exe
                if (!string.IsNullOrEmpty(args[4])) 
                    jsonFile = args[4];
            }

            // ФИКС: Проверка файла с учетом регистра (для Linux)
            if (!File.Exists(jsonFile))
            {
                string jsonFileUpper = Path.Combine(exeDir, "GML.json");
                if (File.Exists(jsonFileUpper))
                    jsonFile = jsonFileUpper;
                else
                {
                    // Попытка найти в текущей директории, если рядом с exe нет
                    string localJson = Path.Combine(workingDir, "gml.json");
                    if (File.Exists(localJson))
                        jsonFile = localJson;
                    else
                    {
                        Console.WriteLine("FATAL ERROR: Could not find gml.json. Searched at:");
                        Console.WriteLine("1. " + Path.Combine(exeDir, "gml.json"));
                        Console.WriteLine("2. " + jsonFileUpper);
                        Console.WriteLine("3. " + localJson);
                        Environment.Exit(1);
                    }
                }
            }

            // Проверка проекта GM
            if (new DirectoryInfo(gmDir).GetFiles("*.yyp").Length == 0)
            {
                Console.WriteLine("FATAL ERROR: No GameMaker project found in {0}", gmDir);
                Environment.Exit(1);
            }

            Console.WriteLine("GML -> C++ conversion begin");
            Console.WriteLine("Using Config: " + jsonFile);
            
            GML.ParseGMLSpec(jsonFile);
            Strings.Add("");

            // Parse sprites
            string spritesPath = Path.Combine(gmDir, "sprites");
            if (Directory.Exists(spritesPath))
            {
                string[] spriteDirs = Directory.GetDirectories(spritesPath);
                foreach (string dir in spriteDirs)
                {
                    Sprite spr = new Sprite(dir, outputSpritesDir);
                    Sprites.Add(spr.Name, spr);
                }
            }

            if (Sprite.TotalCopied > 0)
                Console.WriteLine(Sprite.TotalCopied + " sprite frames were copied");
            else
                Console.WriteLine("No sprites were updated");

            // Parse shaders
            string shadersPath = Path.Combine(gmDir, "shaders");
            if (Directory.Exists(shadersPath))
            {
                string[] shaderDirs = Directory.GetDirectories(shadersPath);
                foreach (string dir in shaderDirs)
                {
                    Shader shader = new Shader(dir, outputShadersDir);
                    if (shader.IsValid)
                        Shaders.Add(shader.Name, shader);
                }
            }

            if (Shader.Modifications.Count > 0)
            {
                Console.WriteLine("The following shaders were modified in the C++ project:");
                foreach (Shader.FileModification mod in Shader.Modifications)
                    Console.WriteLine("    " + mod.Source);

                // В CI/CD мы не можем читать консоль, пропускаем вопрос
                // Console.WriteLine("Do you want to copy them over to the GameMaker project? (y/n)");
                // string input = Console.ReadLine();
            }

            if (Shader.TotalCopied > 0)
                Console.WriteLine(Shader.TotalCopied + " shaders were copied");
            else
                Console.WriteLine("No shaders were updated");

            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Parse script GML
            string scriptsPath = Path.Combine(gmDir, "scripts");
            if (Directory.Exists(scriptsPath))
            {
                string[] scriptDirs = Directory.GetDirectories(scriptsPath);
                foreach (string dir in scriptDirs)
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(dir);
                    string scriptFile = Path.Combine(dir, dirInfo.Name + ".gml");
                    FileInfo gmlInfo = new FileInfo(scriptFile);
                    if (!gmlInfo.Exists)
                        continue;

                    GML.ParseGMLScript(gmlInfo.FullName);
                }
            }

            timer.Stop();
            Console.WriteLine("Parsed GML ({0} lines) in {1}ms", GML.TotalLines, (int)timer.Elapsed.TotalMilliseconds);

            // Parse objects
            string objectsPath = Path.Combine(gmDir, "objects");
            if (Directory.Exists(objectsPath))
            {
                string[] objectDirs = Directory.GetDirectories(objectsPath);
                foreach (string dir in objectDirs)
                {
                    Object obj = new Object(dir);
                    Objects[obj.Name] = obj;
                }
            }

            if (!Objects.ContainsKey("app"))
            {
                Console.WriteLine("FATAL ERROR: app object not found");
                Environment.Exit(1);
            }

            // Create constructors/destructors for GM objects
            foreach (Object obj in Objects.Values)
            {
                obj.SetConstructor(new Function(obj.Name));
                Functions.Add(obj.Name, obj.Constructor);
                if (obj.CreateFunction != null && obj.Name != "app")
                {
                    Function.CurrentParseFunction = obj.Constructor;
                    obj.Constructor.Statements.AddStatement(new CallStatement(new Accessor(obj.CreateFunction.Name, new List<Accessor.ArrayAccessor>(), new List<Expression>(), null, Token.Type.Unknown, 1), 1));
                }

                if (obj.DestroyFunction != null)
                {
                    obj.SetDestructor(new Function(obj.Name));
                    Functions.Add("~" + obj.Name, obj.Destructor);
                    Function.CurrentParseFunction = obj.Destructor;
                    obj.Destructor.Statements.AddStatement(new CallStatement(new Accessor(obj.DestroyFunction.Name, new List<Accessor.ArrayAccessor>(), new List<Expression>(), null, Token.Type.Unknown, 1), 1));
                }
            }

            Objects["app"].Constructor.CppLinesBegin.Add("global::_app = this;");

            timer.Restart();
            foreach (Function func in Functions.Values)
                func.ParseTokens();
            timer.Stop();
            Console.WriteLine("Parsed tokens in {0}ms", (int)timer.Elapsed.TotalMilliseconds);

            if (Functions.ContainsKey("enums"))
                Functions.Remove("enums");
            if (Functions.ContainsKey("macros"))
                Functions.Remove("macros");

            foreach (MacroStatement macro in Macros.Values)
                macro.Resolve(new ResolveScope("global"));

            foreach (DeclareStatement declStmt in DeclareStatement.GlobalDeclarations)
                foreach (Declaration decl in declStmt.Declarations.Declarations)
                    DeclareVariable("global", decl.Name, new DataType(), declStmt.Func, null, declStmt.Line);

            timer.Start();
            ResolveProject();
            WithStatement.ResolveUnknownScope = true;
            Accessor.ResolveFunctionReferences = true;
            Accessor.ResolveUnknownScope = true;
            Accessor.ResolveUnknownMapTypes = true;
            Function.EnableAssignScope = true;
            MergeUnknownVars = true;
            ResolveProject();
            timer.Stop();

            int percResolved = Variable.TotalVariables > 0 ? (int)((1.0f - Variable.VariantVariables / (float)Variable.TotalVariables) * 100.0f) : 100;
            Console.WriteLine("Solved {0} out of {1} variable types ({2}%) in {3}ms", Variable.TotalVariables - Variable.VariantVariables, Variable.TotalVariables, percResolved,(int)timer.Elapsed.TotalMilliseconds);

            timer.Restart();

            if (genGmlFunc)
                GML.ExportHeader(Path.Combine(outputCodeDir, "GmlFunc.hpp"));

            CodeWriter.Begin();
            CodeWriter.WriteLine("#pragma once");
            CodeWriter.WriteLine("#include \"GmlFunc.hpp\"");
            CodeWriter.WriteLine();
            CodeWriter.WriteLine("namespace CppProject");
            CodeWriter.WriteLine("{", 1);

            foreach (MacroStatement macroStmt in Macros.Values)
                macroStmt.WriteCpp(new ResolveScope("global"));
            CodeWriter.WriteLine();

            foreach (EnumStatement enumStmt in Enums.Values)
            {
                enumStmt.WriteCpp(new ResolveScope("global"));
                CodeWriter.WriteLine();
            }

            CodeWriter.WriteLine("struct app;");
            CodeWriter.WriteLine("struct global");
            CodeWriter.WriteLine("{", 1);
            CodeWriter.WriteLine("static app* _app;");
            foreach (Variable var in GlobalVars.Values)
                if (!Macros.ContainsKey(var.Name))
                    CodeWriter.WriteLine("static " + var.Type.ToCpp() + " " + var.Name + ";");
            CodeWriter.WriteLine("};", -1);
            CodeWriter.WriteLine();

            foreach (Object obj in Objects.Values)
            {
                obj.WriteCppHeader();
                CodeWriter.WriteLine();
            }

            foreach (Function func in Functions.Values)
                if (func.StructObject == null)
                    func.WriteCppHeader();

            DataType.IgnoreAllVarType = true;
            if (ExternalFunctions.Count > 0)
                CodeWriter.WriteLine();
            foreach (ExternalFunction extFunc in ExternalFunctions.Values)
                extFunc.WriteCppHeader();
            DataType.IgnoreAllVarType = false;

            int assetId = 20;
            CodeWriter.WriteLine();
            foreach (Object obj in Objects.Values)
                CodeWriter.WriteLine("#define ID_" + obj.Name + " IntType(" + (assetId++) + ")");

            CodeWriter.WriteLine();
            foreach (Sprite sprite in Sprites.Values)
                CodeWriter.WriteLine("#define ID_" + sprite.Name + " IntType(" + (assetId++) + ")");

            CodeWriter.WriteLine();
            foreach (Shader shader in Shaders.Values)
                CodeWriter.WriteLine("#define ID_" + shader.Name + " IntType(" + (assetId++) + ")");

            CodeWriter.WriteLine();
            foreach (Function func in Functions.Values)
                if (func.StructObject == null)
                    CodeWriter.WriteLine("#define ID_" + func.Name + " IntType(" + (assetId++) + ")");

            List<string> members = new List<string>();
            foreach (Object obj in Objects.Values)
            {
                foreach (string varName in obj.InstanceVars.Keys)
                {
                    string cppName = CodeObject.NameToCpp(varName);
                    if (!members.Contains(cppName))
                        members.Add(cppName);
                }
                foreach (string funcName in obj.InstanceFunctions.Keys)
                {
                    string cppName = CodeObject.NameToCpp(funcName);
                    if (!members.Contains(cppName))
                        members.Add(cppName);
                }
            }

            foreach (string varName in UnknownScopeVars.Keys)
            {
                string cppName = CodeObject.NameToCpp(varName);
                if (!members.Contains(cppName))
                    members.Add(cppName);
            }

            assetId = 0;
            members.Sort();
            CodeWriter.WriteLine();
            foreach (string mem in members)
                CodeWriter.WriteLine("#define M_" + mem + " IntType(" + (assetId++) + ")");

            CodeWriter.WriteLine();

            CodeWriter.WriteLine("}", -1);
            CodeWriter.End(Path.Combine(outputCodeDir, "Scripts.hpp"));

            CodeWriter.Begin();
            CodeWriter.WriteLine("#include \"Scripts.hpp\"");
            CodeWriter.WriteLine("#include \"Asset/Shader.hpp\"");
            CodeWriter.WriteLine();
            CodeWriter.WriteLine("namespace CppProject");
            CodeWriter.WriteLine("{", 1);

            DataType.IgnoreAllVarType = true;
            foreach (string var in GML.Variables.Keys)
                if (!GML.Keywords.Contains(var) && var != "argument" && var != "argument_count")
                    CodeWriter.WriteLine(GML.Variables[var].ToCpp() + " gmlGlobal::" + var + " = " + GML.Variables[var].ToCppDefaultValue() + ";");
            CodeWriter.WriteLine();
            DataType.IgnoreAllVarType = false;

            CodeWriter.WriteLine("app* global::_app = nullptr;");
            foreach (Variable var in GlobalVars.Values)
                if (!Macros.ContainsKey(var.Name))
                    CodeWriter.WriteLine(var.Type.ToCpp() + " global::" + var.Name + " = " + var.Type.ToCppDefaultValue() + ";");
            CodeWriter.WriteLine();

            CodeWriter.WriteLine("}", -1);
            CodeWriter.End(Path.Combine(outputCodeDir, "Globals.cpp"));

            const int maxLinePerFile = 1000;
            int f = 1;
            while (true)
            {
                CodeWriter.Begin();
                CodeWriter.WriteLine("#include \"Scripts.hpp\"");
                CodeWriter.WriteLine();
                CodeWriter.WriteLine("namespace CppProject");
                CodeWriter.WriteLine("{", 1);

                foreach (Object obj in Objects.Values)
                {
                    obj.WriteCppImplementation();
                    if (CodeWriter.Lines > maxLinePerFile)
                        break;
                }

                int funcWritten = 0;
                if (CodeWriter.Lines <= maxLinePerFile)
                {
                    foreach (Function func in Functions.Values)
                    {
                        if (func.StructObject != null || func.IsCppSeparate)
                            continue;

                        if (func.WriteCppImplementation())
                            funcWritten++;

                        if (CodeWriter.Lines > maxLinePerFile)
                            break;
                    }

                    if (funcWritten == 0)
                        break; 
                }

                CodeWriter.WriteLine("}", -1);
                CodeWriter.End(Path.Combine(outputCodeDir, "Scripts" + f + ".cpp"));
                f++;
            }

            while (true)
            {
                FileInfo file = new FileInfo(Path.Combine(outputCodeDir, "Scripts" + f + ".cpp"));
                if (file.Exists)
                    file.Delete();
                else
                    break;
                f++;
            }

            CodeWriter.Begin();
            CodeWriter.WriteLine("#include \"Asset/Script.hpp\"");
            CodeWriter.WriteLine("#include \"Asset/Shader.hpp\"");
            CodeWriter.WriteLine("#include \"Asset/Sprite.hpp\"");
            CodeWriter.WriteLine("#include \"Scripts.hpp\"");
            CodeWriter.WriteLine();
            CodeWriter.WriteLine("#define AddSprite(name, id, numFrames, originX, originY) \\", 1);
            CodeWriter.WriteLine("new Sprite(name, id, numFrames, { originX, originY });");
            CodeWriter.Indent(-1);
            CodeWriter.WriteLine();
            CodeWriter.WriteLine("#define AddShader(name, id) \\", 1);
            CodeWriter.WriteLine("new Shader(name, id);");
            CodeWriter.Indent(-1);
            CodeWriter.WriteLine();
            CodeWriter.WriteLine("#define AddScript(name, id, ...) \\", 1);
            CodeWriter.WriteLine("new Script(name, id, [&](IntType s, IntType o, VarArgs a) __VA_ARGS__);");
            CodeWriter.Indent(-1);
            CodeWriter.WriteLine();
            CodeWriter.WriteLine("#define DefineObjectMember(subAssetId, memberId, member, enumType) \\", 1);
            CodeWriter.WriteLine("memberMap[subAssetId][memberId] = { enumType, (long long)&member - (long long)this };");
            CodeWriter.Indent(-1);
            CodeWriter.WriteLine();
            CodeWriter.WriteLine("#define DefineObjectFunction(subAssetId, funcId, ...) \\", 1);
            CodeWriter.WriteLine("instanceFuncMap[funcId] = [&](VarArgs a) __VA_ARGS__;");
            CodeWriter.Indent(-1);
            CodeWriter.WriteLine();
            CodeWriter.WriteLine("namespace CppProject");
            CodeWriter.WriteLine("{", 1);
            CodeWriter.WriteLine("void Asset::Load()");
            CodeWriter.WriteLine("{", 1);

            foreach (Sprite spr in Sprites.Values)
                CodeWriter.WriteLine("AddSprite(\"" + spr.Name + "\", ID_" + spr.Name + ", " + spr.NumFrames + ", " + spr.OriginX + ", " + spr.OriginY + ");");

            CodeWriter.WriteLine();
            foreach (Shader shader in Shaders.Values)
                CodeWriter.WriteLine("AddShader(\"" + shader.Name + "\", ID_" + shader.Name + ");");

            CodeWriter.WriteLine();
            foreach (Function func in Functions.Values)
                if (func.StructObject == null && !func.Name.StartsWith("builder_add_"))
                    CodeWriter.WriteLine("AddScript(\"" + func.Name + "\", ID_" + func.Name + ", " + func.ToExecuteCpp() + ");");

            CodeWriter.WriteLine("}", -1);
            CodeWriter.WriteLine();

            foreach (Object obj in Objects.Values)
                obj.WriteInitMembers();

            CodeWriter.WriteLine("void StringType::AddGMLStrings()");
            CodeWriter.WriteLine("{", 1);
            foreach (string str in Strings)
                CodeWriter.WriteLine("Add(\"" + str + "\");");
            CodeWriter.WriteLine("}", -1);

            CodeWriter.WriteLine("}", -1);
            CodeWriter.End(Path.Combine(outputCodeDir, "Mappings.cpp"));

            Console.WriteLine("Generated code ({0} lines) in {1}ms", CodeWriter.TotalLines, (int)timer.Elapsed.TotalMilliseconds);
            Console.WriteLine("{0} files were updated", CodeWriter.TotalFilesUpdated);
            PrintDebugFiles();

            if (SyntaxErrors.Count > 0)
            {
                foreach (string err in SyntaxErrors)
                    Console.WriteLine("ERROR: " + err);
                Console.WriteLine("Finished with " + SyntaxErrors.Count + " errors");
            }
            else
                Console.WriteLine("Success!");
        }
        
        // ... (Методы ResolveProject, FindVariable и прочие оставь как есть ниже) ...
        // Но чтобы код был полным, я вставлю остаток класса, чтобы ты мог просто скопировать и вставить ВСЁ.
        
        public static void ResolveProject()
		{
			// Mark unresolved
			foreach (Function func in Functions.Values)
				func.ScopesTraversed.Clear();
			foreach (Object obj in Objects.Values)
				foreach (Function func in obj.InstanceFunctions.Values)
					func.ScopesTraversed.Clear();

			CodeObject.UnknownScopes = 0;
			UnknownScopeVars.Clear();
			Console.WriteLine("Processing types...");

			while (true)
			{
				int passVariants = Variable.VariantVariables;
				foreach (Function func in Functions.Values)
					func.IsTraversed = false;
				foreach (Object obj in Objects.Values)
					foreach (Function func in obj.InstanceFunctions.Values)
						func.IsTraversed = false;

				// Resolve object constructors/destructors
				foreach (Object obj in Objects.Values)
				{
					if (obj.Name == "app")
						continue;

					if (obj.Constructor != null)
						obj.Constructor.Resolve(new ResolveScope(obj.Name));

					if (obj.Destructor != null)
						obj.Destructor.Resolve(new ResolveScope(obj.Name));
				}

				Objects["app"].Constructor.Resolve(new ResolveScope("app"));

				// Resolve app functions
				if (AppDrawFunction != null)
					AppDrawFunction.Resolve(new ResolveScope("app"));
				if (AppStepFunction != null)
					AppStepFunction.Resolve(new ResolveScope("app"));
				if (AppHttpFunction != null)
					AppHttpFunction.Resolve(new ResolveScope("app"));
				if (AppGameEndFunction != null)
					AppGameEndFunction.Resolve(new ResolveScope("app"));

				// Resolve instance functions
				foreach (Object obj in Objects.Values)
					foreach (Function func in obj.InstanceFunctions.Values)
						func.Resolve(new ResolveScope(obj.Name));

				if (MergeUnknownVars)
				{
					// Merge instance variables with other instance variables of same name declared in the same function scope
					foreach (Function func in Functions.Values)
					{
						for (int v = 0; v < func.InstanceVarDecls.Count; v++)
						{
							Variable v1 = func.InstanceVarDecls[v];
							for (int vv = 0; vv < func.InstanceVarDecls.Count; vv++)
							{
								if (v == vv)
									continue;

								Variable v2 = func.InstanceVarDecls[vv];
								if (v2.Name == v1.Name)
									v2.Type.Assign(v1.Type, func, 0);
							}
						}
					}

					// Merge unknown variables with instnace variables of same name
					foreach (Variable unknownVar in UnknownScopeVars.Values)
					{
						// Apply instance variables to unknown
						foreach (Object obj in Objects.Values)
							foreach (Variable instVar in obj.InstanceVars.Values)
								if (unknownVar.Name == instVar.Name)
									unknownVar.AssignType(instVar.Type, null, 0);

						// Apply unknown variables to instances
						foreach (Object obj in Objects.Values)
							foreach (Variable instVar in obj.InstanceVars.Values)
								if (unknownVar.Name == instVar.Name)
									instVar.AssignType(unknownVar.Type, null, 0);
					}
				}

				// Nothing was changed, break pass
				if (Variable.VariantVariables == passVariants)
					break;
			}

			// Resolve unused functions in any scope
			if (Accessor.ResolveFunctionReferences)
			{
				foreach (Function func in Functions.Values)
				{
					if (func.IsUnused)
					{
						// Ensure scope is correct for block scripts
						if (func.Name.StartsWith("block_set_") || func.Name.StartsWith("block_tile_entity_"))
							func.Resolve(new ResolveScope("obj_builder_thread"));
						else
							func.Resolve(new ResolveScope("any"));
					}
				}
			}
		}

		public static Variable FindVariable(string scope, string name, Function func, Statement.Location location, int line, Function funcAssignScope = null, bool includeUnknown = true)
		{
			if (func != null)
			{
				for (int i = func.Vars.Count - 1; i >= 0; i--)
				{
					Variable var = func.Vars[i];
					if (var.Name == name && var.Location.Contains(location)) 
					{
						if (var.Line == 0 && func.VarArgs && !func.VarArgsRequiredNames.Contains(name))
							func.VarArgsRequiredNames.Add(name);
						return var;
					}
				}

				if (name != "argument" && name != "argument_count" && name.StartsWith("argument"))
				{
					int argNum = Convert.ToInt32(name.Replace("argument", ""));
					if (func.Vars.Count > argNum && func.Vars[argNum].Line == 0)
						return func.Vars[argNum];
				}
			}

			if (scope == "any" && includeUnknown && UnknownScopeVars.ContainsKey(name))
			{
				if (funcAssignScope != null)
					funcAssignScope.AssignScope(scope, func, line, true);
				return UnknownScopeVars[name];
			}

			if (Objects.ContainsKey(scope))
			{
				if (Objects[scope].InstanceVars.ContainsKey(name))
				{
					Variable var = Objects[scope].InstanceVars[name];
					if (funcAssignScope != null)
					{
						if (!funcAssignScope.InstanceVarDecls.Contains(var)) 
							funcAssignScope.InstanceVarDecls.Add(var);
						funcAssignScope.AssignScope(scope, func, line, true);
					}
					return var;
				}
			}

			if (GlobalVars.ContainsKey(name))
				return GlobalVars[name];

			return null;
		}
		
		public static Variable DeclareVariable(string scope, string name, DataType type, Function func, Statement.Location location, int line = 0, Function funcAssignScope = null)
		{
			if (scope == "any")
			{
				if (!UnknownScopeVars.ContainsKey(name))
					UnknownScopeVars.Add(name, new Variable(scope, name, type, line, location));
				else
					UnknownScopeVars[name].AssignType(type, func, line);
				if (funcAssignScope != null)
					funcAssignScope.AssignScope(scope, func, line, true);
				return UnknownScopeVars[name];
			}

			if (scope == "global")
			{
				if (!GlobalVars.ContainsKey(name))
					GlobalVars.Add(name, new Variable(scope, name, type, line, location));
				else
					GlobalVars[name].AssignType(type, func, line);

				return GlobalVars[name];
			}

			if (Objects.ContainsKey(scope) && line > 0)
			{
				if (!Objects[scope].InstanceVars.ContainsKey(name))
					Objects[scope].InstanceVars.Add(name, new Variable(scope, name, type, line, location));
				else
					Objects[scope].InstanceVars[name].AssignType(type, func, line);

				Variable var = Objects[scope].InstanceVars[name];
				if (funcAssignScope != null)
				{
					if (!funcAssignScope.InstanceVarDecls.Contains(var)) 
						funcAssignScope.InstanceVarDecls.Add(var);
					funcAssignScope.AssignScope(scope, func, line, true);
				}
				return var;
			}

			if (func != null)
			{
				foreach (Variable var in func.Vars)
				{
					if (var.Name != name)
						continue;
					
					if (var.Line == line)  
					{
						var.AssignType(type, func, line);
						return var;
					}
					else if (var.Location.Equals(location)) 
					{
						AddSyntaxError("Variable " + name + " redeclared, previous on line " + var.Line + " in " + func.Name + ":" + line);
						return var;
					}
				}

				Variable newVar = new Variable(scope, name, type, line, location);
				func.Vars.Add(newVar);
				return newVar;
			}

			return null;
		}

		public static void AddSyntaxError(string text)
		{
			if (!SyntaxErrors.Contains(text))
				SyntaxErrors.Add(text);
		}

		static void PrintDebugFiles()
		{
			List<string> globalVarsStrings = new List<string>();
			foreach (Variable var in GlobalVars.Values)
				globalVarsStrings.Add(var.Type.ToCpp() + " " + var.Name + "\n" + var.Type.GetAssignmentsString("\t"));

			List<string> unknownVarsStrings = new List<string>();
			foreach (Variable var in UnknownScopeVars.Values)
				unknownVarsStrings.Add(var.Type.ToCpp() + " " + var.Name + "\n" + var.Type.GetAssignmentsString("\t"));

			List<string> funcsStrings = new List<string>();
			foreach (Function func in Functions.Values)
				funcsStrings.Add(func.ToDebugString());

			List<string> objsStrings = new List<string>();
			foreach (Object obj in Objects.Values)
				objsStrings.Add(obj.ToDebugString());

			globalVarsStrings.Sort();
			unknownVarsStrings.Sort();
			funcsStrings.Sort();
			objsStrings.Sort();
			string globalVarsText = "";
			string unknownVarsText = "";
			string funcsText = "";
			string objsText = "";
			foreach (string var in globalVarsStrings)
				globalVarsText += var;
			foreach (string var in unknownVarsStrings)
				unknownVarsText += var;
			foreach (string func in funcsStrings)
				funcsText += func + "\n";
			foreach (string obj in objsStrings)
				objsText += obj + "\n";

			string logDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
			Directory.CreateDirectory(logDir);
			Console.WriteLine("Writing logs to {0}", logDir);
			File.WriteAllText(Path.Combine(logDir, "globalVars.log"), globalVarsText);
			File.WriteAllText(Path.Combine(logDir, "unknownVars.log"), unknownVarsText);
			File.WriteAllText(Path.Combine(logDir, "funcs.log"), funcsText);
			File.WriteAllText(Path.Combine(logDir, "objs.log"), objsText);
		}
    }
}