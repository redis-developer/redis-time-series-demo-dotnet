using StackExchange.Redis;
using NRedisTimeSeries;
using NRedisTimeSeries.Commands.Enums;
using NRedisTimeSeries.DataTypes;

var muxer = ConnectionMultiplexer.Connect("localhost");
var db = muxer.GetDatabase();
var aggregations = new TsAggregation[]{TsAggregation.Avg, TsAggregation.Min, TsAggregation.Max};

if(!await db.KeyExistsAsync("sensor")){
    await db.TimeSeriesCreateAsync("sensor", 60000, new List<TimeSeriesLabel>{new TimeSeriesLabel("id", "sensor-1")});
    
    foreach(var agg in aggregations)
    {
        await db.TimeSeriesCreateAsync($"sensor:{agg}", 60000, new List<TimeSeriesLabel>{new ("type", agg.ToString()), new TimeSeriesLabel("aggregation-for", "sensor-1")});
        await(db.TimeSeriesCreateRuleAsync("sensor", new TimeSeriesRule($"sensor:{agg}", 5000, agg)));
    }
    
}

var producerTask = Task.Run(async()=>{
    while(true)
    {
        await db.TimeSeriesAddAsync("sensor", "*", Random.Shared.Next(50));
        await Task.Delay(1000);
    }    
});

var consumerTask = Task.Run(async()=>{
    while(true)
    {
        await Task.Delay(1000);
        var result = await db.TimeSeriesGetAsync("sensor");
        Console.WriteLine($"{result.Time.Value}: {result.Val}");
    }
});

var aggregationConsumerTask = Task.Run(async()=>
{
    while(true)
    {
        await Task.Delay(5000);
        var results = await db.TimeSeriesMGetAsync(new List<string>(){"aggregation-for=sensor-1"}, true);
        foreach(var result in results)
        {
            Console.WriteLine($"{result.labels.First(x=>x.Key == "type").Value}: {result.value.Val}");
        }        
        
    }
});



await Task.WhenAll(producerTask, consumerTask, aggregationConsumerTask);