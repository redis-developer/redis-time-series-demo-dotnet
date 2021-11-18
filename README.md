# Basic Redis Time Series with .NET Demo

This demo app contains an intentionally simplistic usage of a Redis Time Series using the [NRedisTimeSeries](https://www.nuget.org/packages/NRedisTimeSeries/) library.

## How to Run

To run this library first startup an instance of RedisTimeSeries:

```
docker run -p 6379:6379 redislabs/redistimeseries
```

Then run the app with `dotnet run`

## How It works

### Create the Time Series and Rules

In this demo we create a simple time series, as well as several time series to handle aggregations from the main time series, and create rules to handle compaction of the time series every 5 seconds:


```csharp
var aggregations = new TsAggregation[]{TsAggregation.Avg, TsAggregation.Min, TsAggregation.Max};

if(!await db.KeyExistsAsync("sensor")){
    await db.TimeSeriesCreateAsync("sensor", 60000, new List<TimeSeriesLabel>{new TimeSeriesLabel("id", "sensor-1")});
    
    foreach(var agg in aggregations)
    {
        await db.TimeSeriesCreateAsync($"sensor:{agg}", 60000, new List<TimeSeriesLabel>{new ("type", agg.ToString()), new TimeSeriesLabel("aggregation-for", "sensor-1")});
        await(db.TimeSeriesCreateRuleAsync("sensor", new TimeSeriesRule($"sensor:{agg}", 5000, agg)));
    }
    
}
```

### Produce Data for the Time Series

Producing data for this Time SEries is left intentionally simplistic, a random number is picked between 0 and 50 and sent into the time series:


```csharp
var producerTask = Task.Run(async()=>{
    while(true)
    {
        await db.TimeSeriesAddAsync("sensor", "*", Random.Shared.Next(50));
        await Task.Delay(1000);
    }    
});
```

### Consuming the Time Series

Then we  handle the consumption of the time series along two separate tasks, 1 to periodically check in on the aggregations, and another to simply consume the time series as it's generated:

```csharp
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
```