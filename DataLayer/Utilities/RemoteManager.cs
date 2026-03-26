using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.Http.Json;
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
            PersistentStorage = StorageType.Test;
            EphemeralStorage = StorageType.Remote;

            // TODO: supply a value for http to automatically replace with a specific address for remote managing


            _httpClient = client;
        }


        public override TResult Query<TEntity, TResult>(
            StorageType storage,
            Expression<Func<IQueryable<TEntity>, TResult>> query,
            int priority = 10)
        {
            var context = GetContext(EphemeralStorage) as RemoteStorage 
                ?? throw new InvalidOperationException("Remote context is not of type: " + typeof(RemoteStorage));
            var provider = new RemoteQueryProvider(context, priority);
            var fakeRoot = provider.CreateQuery<TEntity>(new List<TEntity>().AsQueryable().Expression).Expression;
            //IQueryable<TEntity> set = context.Set<TEntity>().AsQueryable();
            var visitor = new ParameterUpdateVisitor(query.Parameters[0], fakeRoot);
            var invokedExpression = visitor.Visit(query.Body);

            // Return the "fake" queryable that points to your Enqueue engine
            return (TResult)(invokedExpression as dynamic);
        }



        protected override async Task<TEntity> SaveNow<TEntity>(StorageType storage, TEntity entity)
        {
            var serialized = new XDocument(LinqExtensions.VisitToXml(Expression.Constant(entity), 0, 0));
            Console.WriteLine("Save Object: " + serialized);

            if (_httpClient == null)
            {
                throw new InvalidOperationException("No Http client.");
            }

            var context = GetContext(storage)
                ?? throw new InvalidOperationException("Database context failed in: " + nameof(SaveNow));
            //var xml = Expression.Constant(results).ToXDocument().ToString();
            //var json = JsonSerializer.Serialize(xml, JsonHelper.Default);
            var baseAddress = (context as RemoteStorage)?.BaseAddress;
            var queryAddress = (!string.IsNullOrEmpty(baseAddress) ? (baseAddress + (!baseAddress.EndsWith('/') ? '/' : "")) : "")
                + "/api/save";

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
    }
}
