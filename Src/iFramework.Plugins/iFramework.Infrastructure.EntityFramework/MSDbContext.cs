﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Reflection;
using IFramework.Domain;
using IFramework.Infrastructure;

namespace IFramework.EntityFramework
{
    public static class QueryableCollectionInitializer
    {
        public static void InitializeQueryableCollections(this MSDbContext context, object entity)
        {
            var dbEntity = entity as Entity;
            if (dbEntity != null)
            {
                ((dynamic)dbEntity).DomainContext = context;
            }
        }
    }

    public class MSDbContext : DbContext
    {
        public MSDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
            InitObjectContext();
        }


        protected void InitObjectContext()
        {
            var objectContext = (this as IObjectContextAdapter).ObjectContext;
            if (objectContext != null)
            {
                objectContext.ObjectMaterialized +=
                                 (s, e) => this.InitializeQueryableCollections(e.Entity);
            }
        }
    }
}
