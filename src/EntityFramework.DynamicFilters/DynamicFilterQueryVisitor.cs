﻿using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace EntityFramework.DynamicFilters
{
    public class DynamicFilterQueryVisitor : DefaultExpressionVisitor
    {
        private readonly DbContext _ContextForInterception;
        private readonly ObjectContext _ObjectContext;

        public DynamicFilterQueryVisitor(DbContext contextForInterception)
        {
            _ContextForInterception = contextForInterception;
            _ObjectContext = ((IObjectContextAdapter)contextForInterception).ObjectContext;
        }

        public override DbExpression Visit(DbFilterExpression expression)
        {
            //  If the query contains it's own filter condition (in a .Where() for example), this will be called
            //  before Visit(DbScanExpression).  And it will contain the Predicate specified in that filter.
            //  Need to inject our dynamic filters here and then 'and' the Predicate.  This is necessary so that
            //  the expressions are properly ()'d.
            //  It also allows us to attach our dynamic filter into the same DbExpressionBinding so it will avoid
            //  creating a new sub-query in MS SQL Server.

            string entityName = expression.Input.Variable.ResultType.EdmType.Name;
            if (!_InjectedDynamicFilters.Contains(entityName))
            {
                var filterList = expression.Input.Variable.ResultType.EdmType.MetadataProperties
                                    .Where(mp => mp.Name.Contains("customannotation:" + DynamicFilterConstants.ATTRIBUTE_NAME_PREFIX))
                                    .Select(m => m.Value as DynamicFilterDefinition);

                var newFilterExpression = BuildFilterExpressionWithDynamicFilters(entityName, filterList, expression.Input, expression.Predicate);
                if (newFilterExpression != null)
                {
                    //  If not null, a new DbFilterExpression has been created with our dynamic filters.
                    return base.Visit(newFilterExpression);
                }
            }

            return base.Visit(expression);
        }

        public override DbExpression Visit(DbScanExpression expression)
        {
            //  This method will be called for all query expressions.  If there is a filter included (in a .Where() for example),
            //  Visit(DbFilterExpression) will be called first and our dynamic filters will have already been included.
            //  Otherwise, we do that here.

            string entityName = expression.Target.Name;
            if (!_InjectedDynamicFilters.Contains(entityName))
            {
                var filterList = expression.Target.ElementType.MetadataProperties
                    .Where(mp => mp.Name.Contains("customannotation:" + DynamicFilterConstants.ATTRIBUTE_NAME_PREFIX))
                    .Select(m => m.Value as DynamicFilterDefinition);

                var baseResult = base.Visit(expression);
                if (filterList.Any())
                {
                    var binding = DbExpressionBuilder.Bind(baseResult);

                    var newFilterExpression = BuildFilterExpressionWithDynamicFilters(entityName, filterList, binding, null);
                    if (newFilterExpression != null)
                    {
                        //  If not null, a new DbFilterExpression has been created with our dynamic filters.
                        return base.Visit(newFilterExpression);
                    }
                }
            }

            return base.Visit(expression);
        }

        //  Track when we inject the dynamic filters for each Entity so we don't do it twice.  If the query includes it's own filter 
        //  (in .Where clause, for example) we need to do it in the Visit(DbFilterExpression) because that will include the predicate 
        //  of that filter.  Otherwise, only Visit(DbScanExpression) will be called so we have to do it there.  Visit(DbScanExpression) 
        //  will ALSO be called if a filter predicate was provided.
        //  Must be tracked per Entity so that child properties that are Include()'d are also filtered correctly.
        private HashSet<string> _InjectedDynamicFilters = new HashSet<string>();

        private DbFilterExpression BuildFilterExpressionWithDynamicFilters(string entityName, IEnumerable<DynamicFilterDefinition> filterList, DbExpressionBinding binding, DbExpression predicate)
        {
            _InjectedDynamicFilters.Add(entityName);

            if (!filterList.Any())
                return null;

            var edmType = binding.VariableType.EdmType as EntityType;
            if (edmType == null)
                return null;

            List<DbExpression> conditionList = new List<DbExpression>();

            HashSet<string> processedFilterNames = new HashSet<string>(); 
            foreach (var filter in filterList)
            {
                if (processedFilterNames.Contains(filter.FilterName))
                    continue;       //  Already processed this filter - attribute was probably inherited in a base class
                processedFilterNames.Add(filter.FilterName);

                if (!string.IsNullOrEmpty(filter.ColumnName))
                {
                    //  Single column equality filter
                    //  Need to map through the EdmType properties to find the actual database/cspace name for the entity property.
                    //  It may be different from the entity property!
                    var edmProp = edmType.Properties.Where(p => p.MetadataProperties.Any(m => m.Name == "PreferredName" && m.Value.Equals(filter.ColumnName))).FirstOrDefault();
                    if (edmProp == null)
                        continue;       //  ???
                    //  database column name is now in edmProp.Name.  Use that instead of filter.ColumnName

                    var columnProperty = DbExpressionBuilder.Property(DbExpressionBuilder.Variable(binding.VariableType, binding.VariableName), edmProp.Name);
                    var param = columnProperty.Property.TypeUsage.Parameter(filter.CreateDynamicFilterName(filter.ColumnName));

                    //  Create an expression to match on the filter value *OR* a null filter value.  Null can be used to disable the filter completely.
                    conditionList.Add(DbExpressionBuilder.Or(DbExpressionBuilder.Equal(columnProperty, param), DbExpressionBuilder.IsNull(param)));
                }
                else if (filter.Predicate != null)
                {
                    //  Lambda expression filter
                    var dbExpression = LambdaToDbExpressionVisitor.Convert(filter, binding, _ObjectContext);
                    conditionList.Add(dbExpression);
                }
            }

            int numConditions = conditionList.Count;
            DbExpression newPredicate; 
            switch (numConditions)
            {
                case 0:
                    return null;

                case 1:
                    newPredicate = conditionList.First();
                    break;

                default:
                    //  Have multiple conditions.  Need to append them together using 'and' conditions.
                    newPredicate = conditionList.First();

                    for (int i = 1; i < numConditions; i++)
                        newPredicate = newPredicate.And(conditionList[i]);
                    break;
            }

            //  'and' the existing Predicate if there is one
            if (predicate != null)
                newPredicate = newPredicate.And(predicate);

            return DbExpressionBuilder.Filter(binding, newPredicate);
        }
    }
}
