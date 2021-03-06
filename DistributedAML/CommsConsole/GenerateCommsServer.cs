﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using NetMQ;

namespace CommsConsole
{
    public class GenerateCommsServer : GeneratorBase
    {
        String outer = @"   
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NetMQ;
using As.Logger;
using As.Shared;

namespace As.Comms.ClientServer
{
    public abstract class _NAME_Server : I_NAME_
    {
        protected IServiceServer server;
        protected _NAME_Server(IServiceServer server)
        {
            this.server= server;
            this.server.OnReceived += OnReceived;
        }

        private NetMQMessage OnReceived(NetMQMessage request)
        {
            var ret = new NetMQMessage();
            var selector = request.Pop();

            try
            {
                switch (selector.ConvertToString())
                {_HANDLERS_
                    default:
                        throw new Exception($""Unexpected selector - {selector}"");
                }
            }
            catch (Exception e)
            {
                L.Trace($""{selector} caused an exception"");
                L.Exception(e);
                ret.AppendEmptyFrame();
                ret.Append($""{selector} caused an exception - '{e.Message}' check server logs for more details"");
            }
            return ret;
        }

        _FUNCTIONS_
    }
}";

        private Type type;
        public GenerateCommsServer(Type type, List<Type> googleTypes,string sourceDirectoryName) : base(sourceDirectoryName + "/ClientServer/" + ModuleFuncs.GetClassName(type) + "Server.cs")
        {
            this.type = type;

        }

        public void GenerateFile()
        {
            List<string> methods = new List<string>();
            foreach (var method in type.GetMethods())
            {
                methods.Add(GenerateMethod(method));
            }

            List<string> handlers = new List<string>();
            foreach (var method in type.GetMethods())
            {
                handlers.Add(GenerateHandler(method));
            }

            O(outer.Replace("_NAME_", ModuleFuncs.GetClassName(type))
                .Replace("_HANDLERS_", handlers.Aggregate("", (x, y) => x + "\n" + y))
                .Replace("_FUNCTIONS_", methods.Aggregate("", (x, y) => x + "\n" + y)));

            Close();
        }


        String GenerateMethod(MethodInfo method)
        {
            var access = "public abstract";
            var ret = "";
            if (IsObjectEnumerable(method.ReturnType))
            { 
                ret = $"IEnumerable<{method.ReturnType.GenericTypeArguments[0].Name}>";
            }
            else
            {
                ret = method.ReturnType.Name;
            }
            var name = method.Name;
            var parameters = method.GetParameters().Select(GenerateSignatureParameter)
                .Select(x => x.Item1 + " " + x.Item2).Aggregate("", (x, y) => x + y + ",");

            if (parameters.Any() && parameters.Last() == ',')
                parameters = parameters.Substring(0, parameters.Length - 1);

            return $"\t\t{access} {ret} {name}({parameters});\n";
        }

        String GenerateHandler(MethodInfo method)
        {
var s = $@"               case ""{method.Name}"":
                {{
                    SUBHANDLER
                    var methodResult={GenerateCallingStub(method)};
                    ADDMETHODRESULTTORET;
                    break;
                }}";

            s = s.Replace("SUBHANDLER", GenerateSubHandler(method));
            s = s.Replace("ADDMETHODRESULTTORET", GenerateAddMethodResultToRet(method));
            return s;
        }

        String GenerateCallingStub(MethodInfo method)
        {
            var name = method.Name;
            var parameters = method.GetParameters().Select(GenerateSignatureParameter)
                .Select(x => x.Item2 ).Aggregate("", (x, y) => x + y + ",");

            if (parameters.Any() && parameters.Last() == ',')
                parameters = parameters.Substring(0, parameters.Length - 1);

            return $"{name}({parameters})";
        }


        String GenerateSubHandler(MethodInfo method)
        {
            String s = "";
            foreach (var c in method.GetParameters())
            {
                if (c.ParameterType == typeof(String))
                    s += $"var {c.Name} = request.Pop().ConvertToString();\n";
                else if (c.ParameterType == typeof(Int32))
                    s += $"var {c.Name} = request.Pop().ConvertToInt32();\n";
                else if (c.ParameterType == typeof(Int64))
                    s += $"var {c.Name} = request.Pop().ConvertToInt64();\n";
                else if (c.ParameterType.IsEnum)
                {
                    s +=
                        $"var {c.Name} = ({c.ParameterType.Name})Enum.Parse(typeof({c.ParameterType.Name}),request.Pop().ConvertToString());\n";
                }
                else if (typeof(IEnumerable).IsAssignableFrom(c.ParameterType))
                {
                    var paramType = c.ParameterType.GenericTypeArguments[0];
                    if (paramType == typeof(String))
                    {
 s += $@"                
                    var {c.Name} = request.UnpackMessageListString();
";
                    }
                    else if (paramType == typeof(Int32))
                    {
                        s += $@"                
                    var {c.Name} = request.UnpackMessageListInt32();
";
                    }
                    else if (paramType == typeof(Int64))
                    {
                        s += $@"                
                    var {c.Name} = request.UnpackMessageListInt64();
";
                    }
                    else if (typeof(IMessage).IsAssignableFrom(paramType))
                    {
                        s += $@"
                        var {c.Name} = request.UnpackMessageList<{paramType}>({paramType}.Parser.ParseDelimitedFrom);
";
                    }
                    else
                    {
                        throw new Exception($"Unexpected list type {paramType.Name} ");
                    }
                }
                else if (IsSupportedObject(c.ParameterType))
                {
                    s +=
                        $"var {c.Name} = request.UnpackMessage<{c.ParameterType.Name}>({c.ParameterType.Name}.Parser.ParseFrom);";
                }
                else
                {
                    throw new Exception($"Unexpected type - {c.ParameterType} - name {c.Name}");
                }

                s += "\t\t\t\t\t";

            }
            return s;
        }

        String GenerateAddMethodResultToRet(MethodInfo mi)
        {
            String s = "";
            if (mi.ReturnType == typeof(String) || mi.ReturnType == typeof(Int32) || mi.ReturnType == typeof(Int64))
            {
                s = "ret.Append(methodResult)";
            }
            else if (mi.ReturnType == typeof(bool))
            {
                s = "ret.Append(Convert.ToInt32(methodResult))";
            }
            else if (typeof(IEnumerable).IsAssignableFrom(mi.ReturnType))
            {
                if (typeof(IMessage).IsAssignableFrom(mi.ReturnType.GenericTypeArguments[0]))
                {
                    var t = mi.ReturnType.GenericTypeArguments[0];
                    s = $"ret.PackMessageList<{t.Name}>(methodResult);";
                }
            }
            else if (IsSupportedObject(mi.ReturnType))
            {
                s = $"ret.PackMessage<{mi.ReturnType.Name}>(methodResult);";
            }
            else
            {
                throw new Exception($"Unexpected return type for {mi.Name} - {mi.ReturnType.Name}");
            }
            return s;
        }

      
    }
}
