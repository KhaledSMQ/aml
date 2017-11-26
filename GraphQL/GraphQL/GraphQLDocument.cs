﻿using System;
using Antlr4.Runtime;
using GraphQL.Interface;
using GraphlLOutput = GraphQL.GraphQLSerializer.GraphlLOutput;

namespace GraphQL
{
    public class GraphQlDocument : IGraphQlDocument
    {
        private GraphQLParser.DocumentContext documentContext;
        private IGraphQlOutput output;
        private GraphQlCustomiseSchema custom;
        public GraphQlDocument(String query)
        {
            var lexer = new GraphQLLexer(new AntlrInputStream(query));

            var cts = new CommonTokenStream(lexer);

            GraphQLParser parser = new GraphQLParser(cts);

            documentContext = parser.document();

        }

        public GraphQLParser.DocumentContext GetDocumentContext()
        {
            return documentContext;
        }

        public IGraphQlDocument CustomiseSchema(GraphQlCustomiseSchema custom)
        {
            this.custom = custom;
            return this;
        }


        public IGraphQlDocument Validate(Type topLevelType)
        {
            if (GraphQlSchemaLoader.GetSchema(topLevelType)
            var z = new GraphQlMainValidation(this,schema,custom);
            this.schema = schema;
            return this;
        }

        public IGraphQlDocument Run(Object topLevelObject)
        {
            if (schema == null)
                throw new Exception("Need to call 'validate' first");

            output = new GraphlLOutput();
            var p = new GraphQlMainExecution(this,schema,output,topLevelObject);
            return this;
        }

        public String GetOutput()
        {
            return output.ToString();
        }
    }
}