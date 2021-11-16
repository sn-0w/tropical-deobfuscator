using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tropical_Deobfuscator
{
    class Program
    {
        public static ModuleDefMD md = null;
        public static Assembly ass = null;

        static void Main(string[] args)
        {
            md = ModuleDefMD.Load(args[0]);
            md.GlobalType.FindOrCreateStaticConstructor().Body.Instructions[0].OpCode = OpCodes.Nop;
            md.GlobalType.FindOrCreateStaticConstructor().Body.Instructions[1].OpCode = OpCodes.Nop;
            ModuleWriterOptions writerOptions1 = new ModuleWriterOptions(md);
            writerOptions1.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            writerOptions1.Logger = DummyLogger.NoThrowInstance;
            md.Write(args[0].Replace(".exe", "1.exe"), writerOptions1);

            args[0] = args[0].Replace(".exe", "1.exe");

            md = ModuleDefMD.Load(args[0]);
            ass = Assembly.LoadFrom(Directory.GetCurrentDirectory() + @"\" + args[0]);
            int typec = 0;
            foreach (var type in md.GetTypes())
            {
                if (!type.IsSpecialName)
                {
                    type.Name = "Type_" + typec;
                    typec++;
                }
                int methc = 0;
                foreach (var method in type.Methods)
                {
                    if (!method.IsSpecialName)
                    {
                        method.Name = "Method_" + methc;
                        methc++;
                    }
                    int argsc = 0;
                    foreach (var param in method.Parameters)
                    {
                        param.Name = "arg" + argsc;
                        argsc++;
                    }
                }

                int fieldc = 0;
                foreach (var field in type.Fields)
                {
                    if (!field.IsSpecialName)
                    {
                        field.Name = "Field_" + fieldc;
                        fieldc++;
                    }
                }
            }

            foreach (var type in md.GetTypes())
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    SizeOfFixer(method);
                }
            }
            cflow_deob._class.fixcflow(md);
            proxyremover.start(md);
            cflow_deob._class.fixcflow(md);
            ModuleWriterOptions writerOptions = new ModuleWriterOptions(md);
            writerOptions.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            writerOptions.Logger = DummyLogger.NoThrowInstance;
            md.Write(args[0].Replace(".exe", "-arctical.exe"), writerOptions);
            Console.WriteLine("finished!");
            Console.ReadLine();
        }



        //This Method solves sizeof(X)
        public static int GetManagedSize(Type type)
        {
            var method = new System.Reflection.Emit.DynamicMethod("GetManagedSizeImpl", typeof(uint), null);

            System.Reflection.Emit.ILGenerator gen = method.GetILGenerator();

            gen.Emit(System.Reflection.Emit.OpCodes.Sizeof, type);
            gen.Emit(System.Reflection.Emit.OpCodes.Ret);

            var func = (Func<uint>)method.CreateDelegate(typeof(Func<uint>));
            return checked((int)func());
        }

        public static void SizeOfFixer(MethodDef method)
        {
            for (int i = 0; i < method.Body.Instructions.Count - 1; i++)
            {
                Instruction instr = method.Body.Instructions[i];
                if (instr.OpCode == OpCodes.Sizeof)
                {
                    Type SizeOfType = Type.GetType(instr.Operand.ToString());
                    if (SizeOfType != null)
                    {
                        instr.OpCode = OpCodes.Ldc_I4;
                        instr.Operand = GetManagedSize(SizeOfType);
                    }
                }
            }
        }
    }

    class proxyremover
    {

        static private readonly List<MethodDef> JunksMethods = new List<MethodDef>();

        public static void start(ModuleDefMD md)
        {
            foreach (TypeDef typeDef in md.GetTypes())
            {
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    if (methodDef.HasBody)
                    {
                        ProcessMethod(typeDef, methodDef);
                    }
                }
            }

            foreach (TypeDef typeDef in md.GetTypes())
            {
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    if (methodDef.HasBody)
                    {
                        ProcessMethod2(typeDef, methodDef);
                    }
                }
            }
            cflow_deob._class.fixcflow(md);
            foreach (TypeDef typeDef in md.GetTypes())
            {
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    if (methodDef.HasBody)
                    {
                        ProcessMethod3(typeDef, methodDef);
                    }
                }
            }
            cflow_deob._class.fixcflow(md);
            foreach (TypeDef typeDef in md.GetTypes())
            {
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    if (methodDef.HasBody)
                    {
                        ProcessMethod4(typeDef, methodDef);
                    }
                }
            }
            RemoveJunksMethods(md);
        }

        private static void RemoveJunksMethods(ModuleDefMD md)
        {

            foreach (TypeDef typeDef in md.GetTypes())
            {
                for (int i = 0; i < typeDef.Methods.Count(); i++)
                { 
                    if(JunksMethods.Contains(typeDef.Methods[i]))
                    {
                        typeDef.Methods.RemoveAt(i);
                        i--;
                    }
                }
            }

        }

        static private void ProcessMethod(TypeDef typeDef, MethodDef method)
        {
            IList<Instruction> instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                try
                {
                    Instruction instruction = instructions[i];
                    if (instruction.OpCode.Equals(OpCodes.Call))
                    {
                        MethodDef methodDef2 = instruction.Operand as MethodDef;
                        if (methodDef2 == null) continue;
                        if (IsProxyCallMethod(typeDef, methodDef2))
                        {
                            bool IsValid = GetProxyData(methodDef2, out OpCode opCode, out object operand);
                            if (IsValid)
                            {
                                instruction.OpCode = opCode;
                                instruction.Operand = operand;


                                if (!JunksMethods.Contains(methodDef2))
                                    JunksMethods.Add(methodDef2);

                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        static private void ProcessMethod2(TypeDef typeDef, MethodDef method)
        {
            IList<Instruction> instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (i == 0) continue;
                try
                {
                    if (instructions[i].OpCode == OpCodes.Call && instructions[i - 1].OpCode == OpCodes.Ldc_I4)
                    {
                        MethodDef methodDef2 = instructions[i].Operand as MethodDef;
                        if (methodDef2 == null) continue;
                        if (methodDef2.GetParamCount() == 1 && methodDef2.GetParams().First() == Program.md.CorLibTypes.Int32 && methodDef2.ReturnType == Program.md.CorLibTypes.Boolean)
                        {
                            if (methodDef2.DeclaringType.Fields.Count() != 1) continue;

                            object retval = Program.ass.Modules.First().ResolveMethod(methodDef2.MDToken.ToInt32()).Invoke(null, new object[] { instructions[i - 1].GetLdcI4Value() });
                            instructions[i].OpCode = OpCodes.Nop;
                            if ((bool)retval)
                            {
                                instructions[i - 1].OpCode = OpCodes.Ldc_I4_1;
                            }
                            else
                            {
                                instructions[i - 1].OpCode = OpCodes.Ldc_I4_0;
                            }
                            
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        static private void ProcessMethod3(TypeDef typeDef, MethodDef method)
        {
            for (int i = 0; i < method.Body.Instructions.Count(); i++)
            {
                var instr = method.Body.Instructions;

                if (i == 0) continue;
                try
                {
                    if (instr[i].OpCode == OpCodes.Call && instr[i - 1].OpCode == OpCodes.Ldc_I4_0)
                    {
                        IMethod methodDef2 = instr[i].Operand as IMethod;

                        if (methodDef2 == null) continue;


                        MethodDef methoddef3 = methodDef2.ResolveMethodDef();
                        if (methoddef3 == null) continue;
                        if (methodDef2.GetParamCount() == 1 && methodDef2.GetParams().First() == Program.md.CorLibTypes.Boolean && methodDef2.MethodSig.RetType == Program.md.CorLibTypes.String)
                        {
                            if (methoddef3.Body.Instructions[0].OpCode != OpCodes.Ldstr) continue;
                            if (methoddef3.Body.Instructions[1].OpCode != OpCodes.Stloc_1) continue;
                            object retval = Program.ass.Modules.First().ResolveMethod(methodDef2.MDToken.ToInt32()).Invoke(null, new object[] { instr[i - 1].GetLdcI4Value() == 0 ? false : true });
                            if (retval is string)
                            {
                                instr[i].OpCode = OpCodes.Nop;
                                instr[i - 1].OpCode = OpCodes.Ldstr;
                                instr[i - 1].Operand = retval;
                                if (!JunksMethods.Contains(methoddef3))
                                    JunksMethods.Add(methoddef3);
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        static private void ProcessMethod4(TypeDef typeDef, MethodDef method)
        {
            for (int i = 0; i < method.Body.Instructions.Count(); i++)
            {
                var instr = method.Body.Instructions;

                if (i == 0) continue;
                try
                {
                    if (instr[i].OpCode == OpCodes.Call)
                    {
                        IMethod methodDef2 = instr[i].Operand as IMethod;
                        if (methodDef2.HasParams()) continue;
                        if (methodDef2 == null) continue;


                        MethodDef methoddef3 = methodDef2.ResolveMethodDef();
                        if (methoddef3 == null) continue;
                        if (!methodDef2.HasParams() && methodDef2.MethodSig.RetType == Program.md.CorLibTypes.Int32)
                        {
                            if (methoddef3.Body.Instructions[0].OpCode != OpCodes.Ldstr) continue;
                            if (methoddef3.Body.Instructions[1].OpCode != OpCodes.Stloc_1) continue;
                            object retval = Program.ass.Modules.First().ResolveMethod(methoddef3.MDToken.ToInt32()).Invoke(null, null);
                            if (retval is int)
                            {
                                instr[i].OpCode = OpCodes.Ldc_I4;
                                instr[i].Operand = retval;
                            }
                            if (!JunksMethods.Contains(methoddef3))
                                JunksMethods.Add(methoddef3);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        
        static private bool GetProxyData(MethodDef method, out OpCode opCode, out object operand)
        {
            opCode = null;
            operand = null;
            if (!method.HasBody)
            {
                return false;
            }
            Instruction[] array = method.Body.Instructions.ToArray();
            int instrcount = array.Length;
            if (array.Length <= 1)
            {
                return false;
            }
            try
            {
                if (array[instrcount - 2].OpCode.Equals(OpCodes.Newobj))
                {
                    opCode = array[instrcount - 2].OpCode;
                    operand = array[instrcount - 2].Operand;
                }
                if (array[instrcount - 2].IsLdcI4() || array[instrcount - 2].OpCode.Equals(OpCodes.Ldc_R4))
                {
                    opCode = array[instrcount - 2].OpCode;
                    operand = array[instrcount - 2].Operand;
                }
                if (array[instrcount - 2].OpCode.Equals(OpCodes.Call))
                {
                    opCode = array[instrcount - 2].OpCode;
                    operand = array[instrcount - 2].Operand;
                }
                if (array[instrcount - 2].OpCode.Equals(OpCodes.Ldstr))
                {
                    opCode = array[instrcount - 2].OpCode;
                    operand = array[instrcount - 2].Operand;
                }
                if (array[instrcount - 2].OpCode.Equals(OpCodes.Callvirt))
                {
                    opCode = array[instrcount - 2].OpCode;
                    operand = array[instrcount - 2].Operand;
                }
                /*if (array[instrcount - 1].OpCode == OpCodes.Ret)
                {
                    if (instrcount != method.Parameters.Count + 2)
                    {
                        return false;
                    }
                    opCode = array[instrcount - 2].OpCode;
                    operand = array[instrcount - 2].Operand;
                }*/

                if (opCode != null)
                    return true;
            }
            catch { }
            return false;
        }

        static private bool IsProxyCallMethod(TypeDef typeDef, MethodDef method)
        {
            return method?.IsStatic == true && method.Body.Instructions.Count() == 2;
        }
    }
}
