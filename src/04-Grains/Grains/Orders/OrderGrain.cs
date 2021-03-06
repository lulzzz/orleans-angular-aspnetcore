﻿using GrainInterfaces.Inventories;
using GrainInterfaces.Orders;
using GrainInterfaces.Products;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrleansSilo.Orders
{
    [StorageProvider(ProviderName = "TableStore")]
    public class OrderGrain : Grain<Order>, IOrder
    {
        private readonly ILogger _logger;

        public OrderGrain(ILogger<OrderGrain> logger)
        {
            _logger = logger;
        }

        public async Task<Order> Create(Order order)
        {
            var products = await GetProductsAsync(order.Items);
            foreach (var item in order.Items)
            {
                item.Product = products.FirstOrDefault(p => p.Id == item.ProductId);
            }
            order.TotalAmount = order.Items.Sum(item => item.Quantity * item.Product.Price);

            State = order;

            await this.TryDispatch(true);
            return State;
        }

        public async Task<Order> TryDispatch(bool isNewOrder)
        {
            // find best inventory
            var gis = GrainFactory.GetGrain<IInventories>(Guid.Empty);
            var inventoryGuid = await gis.GetBestForProduct(State.Items.First().ProductId);
            var gi = GrainFactory.GetGrain<IInventory>(inventoryGuid);

            // try to dispatch all items, otherwise set order as "not processable"
            var isOrderProcessable = true;
            foreach (var item in State.Items)
            {
                if (isNewOrder)
                {
                    var produckStockRemaining = await gi.Deduct(item.ProductId, item.Quantity);
                    var isItemProcessable = (produckStockRemaining >= 0);
                    if(!isItemProcessable)
                    {
                        // TODO: send event for non-processable order item
                    }
                    isOrderProcessable &= isItemProcessable;
                }
                else
                {
                    var s = await gi.GetProductState(item.ProductId);
                    if (s.CurrentStockQuantity < item.Quantity)
                    {
                        isOrderProcessable = false;
                        break;
                    }
                }
            }
            if (!isNewOrder && isOrderProcessable)
            {
                foreach (var item in State.Items)
                {
                    var produckStockRemaining = await gi.Deduct(item.ProductId, item.Quantity);
                    var isItemProcessable = (produckStockRemaining >= 0);
                    if (!isItemProcessable) { }
                    isOrderProcessable &= isItemProcessable;
                }
            }

            if (isOrderProcessable)
            {
                // TODO: send event for processable order
            }

            State.Dispatched = isOrderProcessable;

            if (State.Dispatched)
            {
                _logger.Info($"Order {State.Id} set as dispatched");
                var gorders = GrainFactory.GetGrain<IOrders>(Guid.Empty);
                await gorders.SetAsDispatched(State.Id);
            }
            await base.WriteStateAsync();
            return State;
        }

        private async Task<Product[]> GetProductsAsync(List<OrderItem> items)
        {
            var products = new List<Task<Product>>();
            foreach (var item in items)
            {
                var product = GrainFactory.GetGrain<IProduct>(item.ProductId);
                products.Add(product.GetState());
            }
            return await Task.WhenAll(products);
        }

        public Task<Order> GetState()
        {
            return Task.FromResult(this.State);
        }
    }
}
