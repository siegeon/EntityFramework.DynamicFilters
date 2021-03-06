﻿using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Interception;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityFramework.DynamicFilters
{
    public static class DynamicFilterExtensions
    {
        #region Privates

        /// <summary>
        /// Key: Filter Name
        /// Value: The parameters for the filter
        /// </summary>
        private static ConcurrentDictionary<string, DynamicFilterParameters> _GlobalParameterValues = new ConcurrentDictionary<string, DynamicFilterParameters>();

        /// <summary>
        /// Key: The DbContext to which the scoped parameter values belong
        /// Values: A dictionary defined as _GlobalParameterValues that contains the scoped parameter values for the DbContext
        /// </summary>
        private static ConcurrentDictionary<DbContext, ConcurrentDictionary<string, DynamicFilterParameters>> _ScopedParameterValues = new ConcurrentDictionary<DbContext, ConcurrentDictionary<string, DynamicFilterParameters>>();

        #endregion

        #region Initialize

        /// <summary>
        /// Initialize the Dynamic Filters.  Call this in OnModelCreating().
        /// </summary>
        /// <param name="context"></param>
        public static void InitializeDynamicFilters(this DbContext context)
        {
            DbInterception.Add(new DynamicFilterCommandInterceptor());
            DbInterception.Add(new DynamicFilterInterceptor());
        }

        #endregion

        #region Add Filters

        #region EntityTypeConfiguration<TEntity> Extensions

        //  These are obsolete and will be removed - use the DbModelBuilder extensions instead

        /// <summary>
        /// Add a filter to a single entity.  Use in OnModelCreating() as:
        ///     modelBuilder.Entity<MyEntity>().Filter(...)
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="config"></param>
        /// <param name="filterName"></param>
        /// <param name="columnName"></param>
        /// <param name="globalValue">If not null, specifies a globally scoped value for this parameter</param>
        /// <returns></returns>
        [Obsolete("Use modelBuilder.Filter() instead")]
        public static EntityTypeConfiguration<TEntity> Filter<TEntity>(this EntityTypeConfiguration<TEntity> config, 
                                                        string filterName, string columnName, object globalValue = null)
            where TEntity : class
        {
            filterName = ScrubFilterName(filterName);
            var filterDefinition = new DynamicFilterDefinition(filterName, null, columnName, typeof(TEntity));

            config.HasTableAnnotation(filterDefinition.AttributeName, filterDefinition);

            if (globalValue != null)
                SetFilterGlobalParameterValue(null, filterName, columnName, globalValue);

            return config;
        }

        /// <summary>
        /// Add a filter to a single entity.  Use in OnModelCreating() as:
        ///     modelBuilder.Entity<MyEntity>().Filter(...)
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="config"></param>
        /// <param name="filterName"></param>
        /// <param name="path"></param>
        /// <param name="globalFuncValue">If not null, specifies a globally scoped value for this parameter as a delegate.</param>
        /// <returns></returns>
        #pragma warning disable 618
        [Obsolete("Use modelBuilder.Filter() instead")]
        public static EntityTypeConfiguration<TEntity> Filter<TEntity, TProperty>(this EntityTypeConfiguration<TEntity> config, 
            string filterName, Expression<Func<TEntity, TProperty>> path, Func<object> globalFuncValue = null)
            where TEntity : class
        {
            return config.Filter(filterName, ParseColumnNameFromExpression(path), globalFuncValue);
        }

        #endregion

        #region Single column equality only filter

        public static void Filter<TEntity, TProperty>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, TProperty>> path, Func<object> globalFuncValue)
            where TEntity : class
        {
            modelBuilder.Filter(filterName, path, (object)globalFuncValue);
        }

        public static void Filter<TEntity, TProperty>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, TProperty>> path, object globalValue = null)
        {
            filterName = ScrubFilterName(filterName);

            //  If ParseColumnNameFromExpression returns null, path is a lambda expression, not a single column expression. 
            LambdaExpression predicate = null;
            string columnName = ParseColumnNameFromExpression(path);
            if (columnName == null)
                predicate = path;

            modelBuilder.Conventions.Add(new DynamicFilterConvention(filterName, typeof(TEntity), predicate, columnName));

            if (globalValue != null)
                SetFilterGlobalParameterValue(null, filterName, columnName, globalValue);
        }

        #endregion

        #region Lambda expression filters

        public static void Filter<TEntity, T0>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, bool>> predicate, Func<T0> value0)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0);
        }

        public static void Filter<TEntity, T0>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, bool>> predicate, T0 value0)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0);
        }

        public static void Filter<TEntity, T0, T1>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, T1, bool>> predicate, 
                                                    Func<T0> value0, Func<T1> value1)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0, (object)value1);
        }

        public static void Filter<TEntity, T0, T1>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, T1, bool>> predicate, 
                                                    T0 value0, T1 value1)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0, (object)value1);
        }

        public static void Filter<TEntity, T0, T1, T2>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, T1, T2, bool>> predicate, 
                                                        Func<T0> value0, Func<T1> value1, Func<T2> value2)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0, (object)value1, (object)value2);
        }

        public static void Filter<TEntity, T0, T1, T2>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, T1, T2, bool>> predicate, 
                                                        T0 value0, T1 value1, T2 value2)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0, (object)value1, (object)value2);
        }

        public static void Filter<TEntity, T0, T1, T2, T3>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, T1, T2, T3, bool>> predicate,
                                                            Func<T0> value0, Func<T1> value1, Func<T2> value2, Func<T3> value3)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0, (object)value1, (object)value2, (object)value3);
        }

        public static void Filter<TEntity, T0, T1, T2, T3>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, T1, T2, T3, bool>> predicate,
                                                            T0 value0, T1 value1, T2 value2, T3 value3)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0, (object)value1, (object)value2, (object)value3);
        }

        public static void Filter<TEntity, T0, T1, T2, T3, T4>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, T1, T2, T3, T4, bool>> predicate,
                                                                Func<T0> value0, Func<T1> value1, Func<T2> value2, Func<T3> value3, Func<T4> value4)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0, (object)value1, (object)value2, (object)value3, (object)value4);
        }

        public static void Filter<TEntity, T0, T1, T2, T3, T4>(this DbModelBuilder modelBuilder, string filterName, Expression<Func<TEntity, T0, T1, T2, T3, T4, bool>> predicate,
                                                                T0 value0, T1 value1, T2 value2, T3 value3, T4 value4)
            where TEntity : class
        {
            Filter<TEntity>(modelBuilder, filterName, predicate, (object)value0, (object)value1, (object)value2, (object)value3, (object)value4);
        }

        private static void Filter<TEntity>(DbModelBuilder modelBuilder, string filterName, LambdaExpression predicate, params object[] valueList)
        {
            filterName = ScrubFilterName(filterName);

            modelBuilder.Conventions.Add(new DynamicFilterConvention(filterName, typeof(TEntity), predicate));

            int numParams = predicate.Parameters == null ? 0 : predicate.Parameters.Count;
            int numValues = valueList == null ? 0 : valueList.Length;
            for (int i = 1; i < numParams; i++)
            {
                object value = ((i - 1) < numValues) ? valueList[i - 1] : null;
                SetFilterGlobalParameterValue(null, filterName, predicate.Parameters[i].Name, value);
            }
        }

        #endregion

        #endregion

        #region Enable/Disable filters

        //  Setting a parameter to null will also disable that parameter

        /// <summary>
        /// Enable the filter.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        public static void EnableFilter(this DbContext context, string filterName)
        {
            var filterParams = GetOrCreateScopedFilterParameters(context, filterName);
            filterParams.Enabled = true;
        }

        /// <summary>
        /// Disable the filter within the current DbContext scope.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        public static void DisableFilter(this DbContext context, string filterName)
        {
            var filterParams = GetOrCreateScopedFilterParameters(context, filterName);
            filterParams.Enabled = false;
        }

        #endregion

        #region Set Filter Parameter Values

        #region Set Scoped Parameter Values

        /// <summary>
        /// Set the parameter for a filter within the current DbContext scope.  Once the DbContext is disposed, this
        /// parameter will no longer be in scope and will be removed.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        /// <param name="func">A delegate that returns the value of the parameter.  This will be evaluated each time
        /// the parameter value is needed.</param>
        public static void SetFilterScopedParameterValue(this DbContext context, string filterName, Func<object> func)
        {
            context.SetFilterScopedParameterValue(filterName, null, (object)func);
        }

        /// <summary>
        /// Set the parameter for a filter within the current DbContext scope.  Once the DbContext is disposed, this
        /// parameter will no longer be in scope and will be removed.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        /// <param name="value"></param>
        public static void SetFilterScopedParameterValue(this DbContext context, string filterName, object value)
        {
            context.SetFilterScopedParameterValue(filterName, null, value);
        }

        /// <summary>
        /// Set the parameter for a filter within the current DbContext scope.  Once the DbContext is disposed, this
        /// parameter will no longer be in scope and will be removed.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        /// <param name="parameterName"></param>
        /// <param name="func"></param>
        public static void SetFilterScopedParameterValue(this DbContext context, string filterName, string parameterName, Func<object> func)
        {
            context.SetFilterScopedParameterValue(filterName, parameterName, (object)func);
        }

        /// <summary>
        /// Set the parameter for a filter within the current DbContext scope.  Once the DbContext is disposed, this
        /// parameter will no longer be in scope and will be removed.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        public static void SetFilterScopedParameterValue(this DbContext context, string filterName, string parameterName, object value)
        {
            var filterParams = GetOrCreateScopedFilterParameters(context, filterName);

            if (string.IsNullOrEmpty(parameterName))
                parameterName = GetDefaultParameterNameForFilter(filterName);

            DynamicFilterParameters globalFilterParams = _GlobalParameterValues[filterName];        //  Already validated that this exists.
            if (!globalFilterParams.ParameterValues.ContainsKey(parameterName))
                throw new ApplicationException(string.Format("Parameter {0} not found in Filter {1}", parameterName, filterName));

            filterParams.SetParameter(parameterName, value);
        }

        #endregion

        #region Set Global Parameter Values

        /// <summary>
        /// Set the parameter value for a filter with global scope.  If a scoped parameter value is not found, this
        /// value will be used.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        /// <param name="func">A delegate that returns the value of the parameter.  This will be evaluated each time
        /// the parameter value is needed.</param>
        public static void SetFilterGlobalParameterValue(this DbContext context, string filterName, Func<object> func)
        {
            context.SetFilterGlobalParameterValue(filterName, (object)func);
        }

        /// <summary>
        /// Set the parameter value for a filter with global scope.  If a scoped parameter value is not found, this
        /// value will be used.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        /// <param name="value"></param>
        public static void SetFilterGlobalParameterValue(this DbContext context, string filterName, object value)
        {
            context.SetFilterGlobalParameterValue(filterName, null, value);
        }

        public static void SetFilterGlobalParameterValue(this DbContext context, string filterName, string parameterName, Func<object> func)
        {
            context.SetFilterGlobalParameterValue(filterName, parameterName, (object)func);
        }

        public static void SetFilterGlobalParameterValue(this DbContext context, string filterName, string parameterName, object value)
        {
            filterName = ScrubFilterName(filterName);

            if (string.IsNullOrEmpty(parameterName))
                parameterName = GetDefaultParameterNameForFilter(filterName);

            _GlobalParameterValues.AddOrUpdate(filterName,
                (f) =>
                {
                    var newValues = new DynamicFilterParameters();
                    newValues.SetParameter(parameterName, value);
                    return newValues;
                },
                (f, currValues) =>
                {
                    currValues.SetParameter(parameterName, value);
                    return currValues;
                });
        }

        #endregion

        #endregion

        #region Get Filter Parameter Values

        /// <summary>
        /// Returns the value for the filter.  If a scoped value exists within this DbContext, that is returned.
        /// Otherwise, a global parameter value will be returned.  If the parameter was set with a delegate, the
        /// delegate is evaluated and the result is returned.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterName"></param>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        public static object GetFilterParameterValue(this DbContext context, string filterName, string parameterName)
        {
            filterName = ScrubFilterName(filterName);
            if (parameterName == null)
                parameterName = string.Empty;

            ConcurrentDictionary<string, DynamicFilterParameters> contextFilters;
            DynamicFilterParameters filterParams;
            object value;

            //  First try to get the value from _ScopedParameterValues
            if (_ScopedParameterValues.TryGetValue(context, out contextFilters))
            {
                if (contextFilters.TryGetValue(filterName, out filterParams))
                {
                    //  If filter is disabled, return null
                    if (!filterParams.Enabled)
                        return null;

                    if (filterParams.ParameterValues.TryGetValue(parameterName, out value))
                    {
                        var func = value as Func<object>;
                        return (func == null) ? value : func();
                    }
                }
            }

            //  Then try _GlobalParameterValues
            if (_GlobalParameterValues.TryGetValue(filterName, out filterParams))
            {
                if (filterParams.ParameterValues.TryGetValue(parameterName, out value))
                {
                    var func = value as MulticastDelegate;
                    return (func == null) ? value : func.DynamicInvoke();
                }
            }

            //  Not found anywhere???
            return null;
        }

        #endregion

        #region Clear Parameter Values

        /// <summary>
        /// Clear all parameter values within the DbContext scope.
        /// </summary>
        /// <param name="context"></param>
        public static void ClearScopedParameters(this DbContext context)
        {
            ConcurrentDictionary<string, DynamicFilterParameters> contextFilters;
            _ScopedParameterValues.TryRemove(context, out contextFilters);

            System.Diagnostics.Debug.Print("Cleared scoped parameters.  Have {0} scopes", _ScopedParameterValues.Count);
        }

        #endregion

        #region Set Sql Parameters

        internal static void SetSqlParameter(this DbContext context, DbParameter param)
        {
            if (!param.ParameterName.StartsWith(DynamicFilterConstants.PARAMETER_NAME_PREFIX))
                return;

            //  parts are:
            //  1 = Fixed string constant (DynamicFilterConstants.PARAMETER_NAME_PREFIX)
            //  2 = Filter Name (delimiter char is scrubbed from this field when creating a filter)
            //  3+ = Column Name (this can contain the delimiter char)
            var parts = param.ParameterName.Split(new string[] { DynamicFilterConstants.DELIMETER }, StringSplitOptions.None);
            if (parts.Length < 3)
                return;

            object value = context.GetFilterParameterValue(parts[1], parts[2]);       //  Middle is the filter name

            //  If not found, leave as the default that EF assigned (which will be a DBNull and will disable the filter)
            if (value != null)
                param.Value = value;
        }

        #endregion

        #region Private Methods

        private static string ScrubFilterName(string filterName)
        {
            //  Do not allow the delimiter char in the filter name at all because it will interfere with us parsing out
            //  the filter name from the parameter name.  Doesn't matter in column name though.
            return filterName.Replace(DynamicFilterConstants.DELIMETER, "");
        }

        private static DynamicFilterParameters GetOrCreateScopedFilterParameters(DbContext context, string filterName)
        {
            filterName = ScrubFilterName(filterName);

            if (!_GlobalParameterValues.ContainsKey(filterName))
                throw new ApplicationException(string.Format("Filter name {0} not found", filterName));

            var newContextFilters = new ConcurrentDictionary<string, DynamicFilterParameters>();
            var contextFilters = _ScopedParameterValues.GetOrAdd(context, newContextFilters);
            var filterParams = contextFilters.GetOrAdd(filterName, (p) => new DynamicFilterParameters());

            if (contextFilters == newContextFilters)
            {
                System.Diagnostics.Debug.Print("Created new scoped filter params.  Have {0} scopes", _ScopedParameterValues.Count);

                //  We created new filter params for this scope.  Add an event handler to the OnDispose to clean them up when
                //  the context is disposed.
                var internalContext = typeof(DbContext)
                    .GetProperty("InternalContext", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetGetMethod(true)
                    .Invoke(context, null);

                var eventInfo = internalContext.GetType().GetEvent("OnDisposing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                eventInfo.AddEventHandler(internalContext, new EventHandler<EventArgs>((o, e) => context.ClearScopedParameters()));
            }

            return filterParams;
        }

        private static string GetDefaultParameterNameForFilter(string filterName)
        {
            //  If the parameter name is not specified, find it from the global parameters.  There must be exactly
            //  1 parameter in the filter for this to work.  If multiple parameters are used, the parameterName
            //  must be specified when the value is set.
            filterName = ScrubFilterName(filterName);

            DynamicFilterParameters globalFilterParams;
            if (!_GlobalParameterValues.TryGetValue(filterName, out globalFilterParams))
                throw new ApplicationException(string.Format("Filter name {0} not found", filterName));

            if (globalFilterParams.ParameterValues.Count != 1)
                throw new ApplicationException("Attempted to set Scoped Parameter without specifying Parameter Name and when filter does not contain exactly 1 parameter");

            return globalFilterParams.ParameterValues.Keys.FirstOrDefault();
        }

        private static string ParseColumnNameFromExpression(LambdaExpression expression)
        {
            if (expression == null)
                throw new ArgumentNullException("Lambda expression is null");

            var body = expression.Body as MemberExpression;
            if ((body == null) || (body.Member == null) || string.IsNullOrEmpty(body.Member.Name))
            {
                //  The expression does not specify a column - it's a lambda expression/predicate that will need to
                //  be expanded by LabdaToDbExprssionVisitor during the query evaluation.
                return null;
            }

            return body.Member.Name;
        }

        #endregion
    }
}
