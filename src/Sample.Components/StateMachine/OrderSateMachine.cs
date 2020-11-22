namespace Sample.Components.StateMachines
{
    using System;
    using Automatonymous;
    using MassTransit;
    using MassTransit.RedisIntegration;
    using MassTransit.Saga;
    using Sample.Contracts;

    public class OrderStateMachine : MassTransitStateMachine<OrderState>
    {
        public OrderStateMachine()
        {
            //Especifica el id por el cual se relaciona el evento/Mensaje
            Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => OrderStatusRequested, x =>{
                 x.CorrelateById(m => m.Message.OrderId);
                 x.OnMissingInstance(m => m.ExecuteAsync(async context => {
                    if(context.RequestId.HasValue){
                        await context.RespondAsync<OrderNotFound>(new {context.Message.OrderId});
                    }
                 }));
            });

            InstanceState(x => x.CurrentState);

            Initially(
                When(OrderSubmitted)
                .Then(context =>
                {
                    context.Instance.SubmitDate = context.Data.Timestamp;
                    context.Instance.CustomerNumber = context.Data.CustomerNumber;
                    context.Instance.Updated = DateTime.UtcNow;
                })
                .TransitionTo(Submitted));

            During(Submitted,
                Ignore(OrderSubmitted));

            DuringAny(
                When(OrderSubmitted)
                    .Then(context =>
                    {
                        context.Instance.SubmitDate = context.Data.Timestamp;
                        context.Instance.CustomerNumber = context.Data.CustomerNumber;
                    })
            );

            DuringAny(
                When(OrderStatusRequested)
                .RespondAsync(x => x.Init<OrderStatus>(new { 
                    OrderId = x.Instance.CorrelationId,
                    State = x.Instance.CurrentState
                }))
            );
        }
        public State Submitted { get; set; }
        public Event<OrderSubmitted> OrderSubmitted { get; set; }
        public Event<CheckOrder> OrderStatusRequested { get; set; }

    }

    public class OrderState : SagaStateMachineInstance, IVersionedSaga
    {
        public Guid CorrelationId { get; set; }
        public string CurrentState { get; set; }
        public DateTime? Updated { get; set; }
        public DateTime? SubmitDate { get; set; }
        public string CustomerNumber { get; set; }
        public int Version { get; set; }

    }

}