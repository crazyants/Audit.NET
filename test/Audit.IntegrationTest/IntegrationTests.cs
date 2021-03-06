﻿using System;
using System.Collections.Generic;
using System.Linq;
using Audit.Core;
using Audit.Core.Providers;
using Audit.MongoDB.Providers;
using Audit.SqlServer.Providers;
using Xunit;
using Newtonsoft.Json.Linq;
#if NET451
using Audit.AzureDocumentDB.Providers;
#endif

namespace Audit.IntegrationTest
{
    public class IntegrationTests
    {
        public class AuditTests
        {
#if NET451
            [Fact]
            public void TestEventLog()
            {
                SetEventLogSettings();
                TestUpdate();
                TestInsert();
                TestDelete();
            }

            [Fact]
            public void TestAzure()
            {
                SetAzureSettings();
                TestUpdate();
                TestInsert();
                TestDelete();
            }
#endif

            [Fact]
            public void TestFile()
            {
                SetFileSettings();
                TestUpdate();
                TestInsert();
                TestDelete();
            }

            [Fact]
            public void TestSql()
            {
                SetSqlSettings();
                TestUpdate();
                TestInsert();
                TestDelete();
            }

            [Fact]
            public void TestMongo()
            {
                SetMongoSettings();
                TestUpdate();
                TestInsert();
                TestDelete();
            }

            public struct TestStruct
            {
                public int Id { get; set; }
                public CustomerOrder Order { get; set; }
            }

            public void TestUpdate()
            {
                var order = DbCreateOrder();
                var reasonText = "the order was updated because ...";
                var eventType = "Order:Update";
                var ev = (AuditEvent)null;
                //struct
                using (var a = AuditScope.Create(eventType, () => new TestStruct() { Id = 123, Order = order }, new { ReferenceId = order.OrderId }))
                {
                    ev = a.Event;
                    a.SetCustomField("$TestGuid", Guid.NewGuid());

                    a.SetCustomField("$null", (string)null);
                    a.SetCustomField("$array.dicts", new[]
                    {
                        new Dictionary<string, string>()
                        {
                            {"some.dots", "hi!"}
                        }
                    });


                    order = DbOrderUpdateStatus(order, OrderStatus.Submitted);
                }
                
                Assert.Equal(Configuration.DataProvider.Serialize(order.OrderId), ev.CustomFields["ReferenceId"]);

                order = DbCreateOrder();

                //audit multiple 
                using (var a = AuditScope.Create(eventType, () => new { OrderStatus = order.Status, Items = order.OrderItems }, new { ReferenceId = order.OrderId }))
                { 
                   ev = a.Event;
                    order = DbOrderUpdateStatus(order, OrderStatus.Submitted);
                }

                Assert.Equal(Configuration.DataProvider.Serialize(order.OrderId), ev.CustomFields["ReferenceId"]);

                order = DbCreateOrder();

                using (var audit = AuditScope.Create("Order:Update", () => order.Status, new { ReferenceId = order.OrderId }))
                {
                    ev = audit.Event;
                    audit.SetCustomField("Reason", reasonText);
                    audit.SetCustomField("ItemsBefore", order.OrderItems);
                    audit.SetCustomField("FirstItem", order.OrderItems.FirstOrDefault());

                    order = DbOrderUpdateStatus(order, IntegrationTests.OrderStatus.Submitted);
                    audit.SetCustomField("ItemsAfter", order.OrderItems);
                    audit.Comment("Status Updated to Submitted");
                    audit.Comment("Another Comment");
                }

                Assert.Equal(Configuration.DataProvider.Serialize(order.OrderId), ev.CustomFields["ReferenceId"]);

                order = DbCreateOrder();

                using (var audit = AuditScope.Create(eventType, () => order, new { ReferenceId = order.OrderId }))
                {
                    ev = audit.Event;
                    audit.SetCustomField("Reason", "reason");
                    ExecuteStoredProcedure(order, IntegrationTests.OrderStatus.Submitted);
                    order.Status = IntegrationTests.OrderStatus.Submitted;
                    audit.Comment("Status Updated to Submitted");
                }

                Assert.Equal(Configuration.DataProvider.Serialize(order.OrderId), ev.CustomFields["ReferenceId"]);
            }

            public void TestInsert()
            {
                var ev = (AuditEvent)null;
                CustomerOrder order = null;
                using (var audit = AuditScope.Create("Order:Create", () => order))
                {
                    ev = audit.Event;
                    order = DbCreateOrder();
                    audit.SetCustomField("ReferenceId", order.OrderId);
                }

                Assert.Equal(Configuration.DataProvider.Serialize(order.OrderId), ev.CustomFields["ReferenceId"]);
            }

            public void TestDelete()
            {
                IntegrationTests.CustomerOrder order = DbCreateOrder();
                var ev = (AuditEvent)null;
                var orderId = order.OrderId;
                using (var audit = AuditScope.Create("Order:Delete", () => order, new { ReferenceId = order.OrderId }))
                {
                    ev = audit.Event;
                    DbDeteleOrder(order.OrderId);
                    order = null;
                }
                Assert.Equal(Configuration.DataProvider.Serialize(orderId), ev.CustomFields["ReferenceId"]);
            }

#if NET451
            public void SetEventLogSettings()
            {
                Audit.Core.Configuration.Setup()
                    .UseEventLogProvider(config => config
                        .LogName("Application")
                        .SourcePath("TestApplication")
                        .MachineName("."))
                    .WithCreationPolicy(EventCreationPolicy.InsertOnStartReplaceOnEnd)
                    .ResetActions();
            }

            public void SetAzureSettings()
            {
                Audit.Core.Configuration.Setup()
                    .UseAzureDocumentDB(config => config
                        .ConnectionString("https://thepirat.documents.azure.com:443/")
                        .AuthKey("xxxxxx=="))
                    .WithCreationPolicy(EventCreationPolicy.InsertOnStartReplaceOnEnd)
                    .ResetActions();
            }
#endif
            public void SetFileSettings()
            {
                Audit.Core.Configuration.Setup()
                    .UseFileLogProvider(config => config.Directory(@"c:\temp\1").FilenamePrefix("Event_"))
                    .WithCreationPolicy(EventCreationPolicy.InsertOnStartReplaceOnEnd)
                    .ResetActions();
            }

            public void SetSqlSettings()
            {
                Audit.Core.Configuration.Setup()
                    .UseSqlServer(config => config
                        .ConnectionString("data source=localhost;initial catalog=Audit;integrated security=true;")
                        .TableName("Event")
                        .IdColumnName("EventId")
                        .JsonColumnName("Data")
                        .LastUpdatedColumnName("LastUpdatedDate"))
                    .WithCreationPolicy(EventCreationPolicy.InsertOnStartReplaceOnEnd)
                    .ResetActions();
            }

            public void SetMongoSettings()
            {
                Audit.Core.Configuration.Setup()
                    .UseMongoDB(config => config
                        .ConnectionString("mongodb://localhost:27017")
                        .Database("Audit")
                        .Collection("Event"))
                    .WithCreationPolicy(EventCreationPolicy.InsertOnStartReplaceOnEnd)
                    .ResetActions();
            }

            public static void ExecuteStoredProcedure(IntegrationTests.CustomerOrder order, IntegrationTests.OrderStatus status)
            {
            }

            public static void DbDeteleOrder(string id)
            {
            }

            public static IntegrationTests.CustomerOrder DbCreateOrder()
            {
                var order = new IntegrationTests.CustomerOrder()
                {
                    OrderId = Guid.NewGuid().ToString(),
                    CustomerId = "customer 123 some 'quotes' to test's. double ''. some double \"quotes\" \"",
                    Status = IntegrationTests.OrderStatus.Created,
                    OrderItems = new List<IntegrationTests.CustomerOrderItem>()
                    {
                        new IntegrationTests.CustomerOrderItem()
                        {
                            Sku = "1002",
                            Quantity = 3
                        }
                    }
                };
                return order;
            }

            public static IntegrationTests.CustomerOrder DbOrderUpdateStatus(IntegrationTests.CustomerOrder order,
                IntegrationTests.OrderStatus newStatus)
            {
                order.Status = newStatus;
                order.OrderItems = null;
                return order;
            }
        }

        public class CustomerOrder
        {
            public string OrderId { get; set; }
            public IntegrationTests.OrderStatus Status { get; set; }
            public string CustomerId { get; set; }
            public IEnumerable<IntegrationTests.CustomerOrderItem> OrderItems { get; set; }
        }

        public class CustomerOrderItem
        {
            public string Sku { get; set; }
            public double Quantity { get; set; }
        }

        public enum OrderStatus
        {
            Created = 2,
            Submitted = 4,
            Cancelled = 10
        }
    }
}
