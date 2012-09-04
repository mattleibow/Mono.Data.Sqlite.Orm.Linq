﻿using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace IQToolkit
{
	public interface IAsyncQueryProvider : IQueryProvider
	{
		Task<object> ExecuteAsync(Expression query);

		Task<TResult> ExecuteAsync<TResult>(Expression query);
	}
}