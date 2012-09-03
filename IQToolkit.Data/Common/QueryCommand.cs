// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IQToolkit.Data.Common
{
    public class QueryCommand
    {
	    public QueryCommand(string commandText, IEnumerable<QueryParameter> parameters)
        {
            this.CommandText = commandText;
            this.Parameters = parameters.ToReadOnly();
        }

	    public string CommandText { get; private set; }

	    public ReadOnlyCollection<QueryParameter> Parameters { get; private set; }
    }

    public class QueryParameter
    {
	    public QueryParameter(string name, Type type, DbQueryType queryType)
        {
            this.Name = name;
            this.Type = type;
            this.QueryType = queryType;
        }

	    public string Name { get; private set; }

	    public Type Type { get; private set; }

	    public DbQueryType QueryType { get; private set; }
    }
}
