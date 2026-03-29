using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.JSInterop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DataLayer.Utilities
{

    public class RemoteManager : QueryManager
    {

        private readonly HttpClient _httpClient;


        public RemoteManager(HttpClient client, IServiceProvider Service) : base(Service)
        {
            PersistentStorage = StorageType.Remote; // TODO: switch these back
            EphemeralStorage = StorageType.Test;

            //var context = GetContext(EphemeralStorage) as RemoteStorage
            //    ?? throw new InvalidOperationException("Remote context is not of type: " + typeof(RemoteStorage));

            // TODO: supply a value for http to automatically replace with a specific address for remote managing
            //FinalProvider = new RemoteQueryProvider(context);

            _httpClient = client;
        }


        protected async Task<TEntity> SaveRemote<TEntity>(RemoteStorage context, TEntity entity)
            where TEntity : class
        {
            var serialized = new XDocument(LinqExtensions.VisitToXml(Expression.Constant(entity), 0, 0));
            Console.WriteLine("Save Object: " + serialized);

            var baseAddress = (context as RemoteStorage)?.BaseAddress?.TrimEnd('/');
            var queryAddress = (!string.IsNullOrEmpty(baseAddress) ? (baseAddress + (!baseAddress.EndsWith('/') ? '/' : "")) : "")
                + "api/save";

            var response = await _httpClient.PostAsJsonAsync(queryAddress, serialized);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<string>();

            using XmlReader reader = XmlReader.Create(new StringReader(content ?? string.Empty));
            _ = reader.MoveToContent();
            XElement root = (XElement)XNode.ReadFrom(reader);

            ConstantExpression? finalExpression = root.ToExpression(context, out IQueryable? set) as ConstantExpression
                ?? throw new InvalidOperationException("Could not convert expression document to Queryable: " + content);

            var resultEntity = finalExpression.Value as TEntity
                ?? throw new InvalidOperationException("Could not render entity from server response.");

            context.Entry(resultEntity).State = EntityState.Detached;

            return resultEntity;
        }



        protected static async Task<TEntity> SaveLocal<TEntity>(TestStorage context, TEntity entity)
            where TEntity : Entity<TEntity>
        {
            if (context.Store == null)
                throw new InvalidOperationException("IDB Module not setup for saving.");
            await context.Store.PutRecordAsync(Entity<TEntity>.Metadata.TableName, entity);
            return entity;
        }



        public override TResult Query<TEntity, TResult>(
            StorageType storage,
            Expression<Func<IQueryable<TEntity>, TResult>> query,
            int priority = 10)
        {
            var context = GetStorageType(storage)
                ?? throw new InvalidOperationException("Database context failed in: " + nameof(SaveNow));

            if (context == typeof(RemoteStorage))
                return base.Query<TEntity, TResult>(storage, query, priority);
            else if (context == typeof(TestStorage))
            {
                // clean now so we know where it came from and don't have to go through task stack chains
                try
                {
                    var cleanExpression = new ClosureEvaluatorVisitor().Visit(query);
                    var visitor = new AggressiveVisitor();
                    var simpleExpression = visitor.Visit(cleanExpression);

                    return base.Query<TEntity, TResult>(storage, (Expression<Func<IQueryable<TEntity>, TResult>>)simpleExpression, priority);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Query will fail: " + query.ToString() + " - " + ex);
                    throw new InvalidOperationException("Query will fail: " + query.ToString() + " - " + ex);
                }

            }
            else
                return base.Query<TEntity, TResult>(storage, query, priority);
        }



        protected override async Task<TEntity> SaveNow<TEntity>(StorageType storage, TEntity entity)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("No Http client.");
            }

            var context = GetContext(storage)
                ?? throw new InvalidOperationException("Database context failed in: " + nameof(SaveNow));

            if (context is RemoteStorage remote)
                return await SaveRemote<TEntity>(remote, entity);
            else if (context is TestStorage test)
                return await SaveLocal<TEntity>(test, entity);
            else
                return await base.SaveNow(storage, entity);

        }
    }
}
