using System.Text.Json;
using System.Text.Json.Serialization;


namespace Core.Api.Maps
{
  public class PragueGeoDataResponseFeature
  {
    public string type { get; set; }
    public PragueGeoDataResponseGeometry geometry { get; set; }
    public PragueGeoDataResponseProperties properties { get; set; }
  }


  public class PragueGeoDataResponseGeometry
  {
    public string type { get; set; }

    [JsonConverter(typeof(CoordinatesConverter))]
    public List<GPSData> coordinates { get; set; }
  }

  public class PragueGeoDataResponseProperties
  {
    public int OBJECTID { get; set; }
    public string route_id { get; set; }
    public string route_short_name { get; set; }
    public string route_long_name { get; set; }
    public string route_type { get; set; }
    public string route_url { get; set; }
    public string route_color { get; set; }
    public string is_night { get; set; }
    public string is_regional { get; set; }
    public string is_substitute_transport { get; set; }
    public string validity { get; set; }
    public double Shape_Length { get; set; }
  }

  public class PragueGeoDataResponse
  {
    public List<PragueGeoDataResponseFeature> features { get; set; }
  }

  public class CoordinatesConverter : JsonConverter<List<GPSData>>
  {
    public override List<GPSData> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      var gpsDataList = new List<GPSData>();

      if (reader.TokenType != JsonTokenType.StartArray)
      {
        throw new JsonException("Expected an array for coordinates.");
      }

      using (var document = JsonDocument.ParseValue(ref reader))
      {
        var firstElement = document.RootElement[0];

        if (firstElement.ValueKind == JsonValueKind.Array && firstElement[0].ValueKind == JsonValueKind.Number)
        {
          // LineString (2D array of doubles)
          var coordinates = JsonSerializer.Deserialize<List<List<double>>>(document.RootElement.GetRawText(), options);
          if (coordinates != null)
          {
            foreach (var point in coordinates)
            {
              gpsDataList.Add(new GPSData(point[1], point[0]));
            }
          }
        }
        else if (firstElement.ValueKind == JsonValueKind.Array && firstElement[0].ValueKind == JsonValueKind.Array)
        {
          // MultiLineString (3D array of doubles)
          var coordinates = JsonSerializer.Deserialize<List<List<List<double>>>>(document.RootElement.GetRawText(), options);
          if (coordinates != null)
          {
            foreach (var line in coordinates)
            {
              foreach (var point in line)
              {
                gpsDataList.Add(new GPSData(point[1], point[0]));
              }
            }
          }
        }
        else
        {
          throw new JsonException("Invalid coordinate structure.");
        }
      }

      return gpsDataList;
    }

    public override void Write(Utf8JsonWriter writer, List<GPSData> value, JsonSerializerOptions options)
    {
      throw new NotImplementedException();
    }
  }

  public class PragueGeoData : GeoDataInterface
  {
    static Dictionary<string, RouteType> routeTypeMap = new Dictionary<string, RouteType>
    {
      { "3", RouteType.Bus },
      { "11", RouteType.Trolleybus },
      { "7", RouteType.Ferry },
      { "1", RouteType.Subway },
      { "2", RouteType.Rail },
      { "0", RouteType.Tram },
    };
    public async Task<GeoData[]> getData(bool useCache = true)
    {
      string date = DateTime.Now.ToString("yyyy-MM-dd");
      string filePath = $"{Util.Util.CheckForCacheDir()}/geodata_{date}.json";
      string content = "";
      if (!File.Exists(filePath) || !useCache)
      {
        string url = "https://data.pid.cz/geodata/Linky_WGS84.json";
        using (HttpClient client = new HttpClient())
        {
          HttpResponseMessage response = await client.GetAsync(url);
          if (!response.IsSuccessStatusCode)
          {
            throw new Exception("Request failed!");
          }
          content = await response.Content.ReadAsStringAsync();
          File.WriteAllText(filePath, content);
        }
      }
      else
      {
        content = File.ReadAllText(filePath);
      }
      JsonSerializerOptions options = new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 64,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
      };
      PragueGeoDataResponse jsonResponse = JsonSerializer.Deserialize<PragueGeoDataResponse>(content, options);
      GeoData[] geoData = new GeoData[jsonResponse.features.Count];

      for (int i = 0; i < jsonResponse.features.Count; i++)
      {
        geoData[i] = new GeoData
        {
          geometry = jsonResponse.features[i].geometry.coordinates,
          routeId = jsonResponse.features[i].properties.route_id,
          routeDisplayNumber = jsonResponse.features[i].properties.route_short_name,
          routeNameLong = jsonResponse.features[i].properties.route_long_name,
          routeColor = jsonResponse.features[i].properties.route_color,
          routeUrl = jsonResponse.features[i].properties.route_url,
          isSubsitute = jsonResponse.features[i].properties.is_substitute_transport == "1",
          isNightRoute = jsonResponse.features[i].properties.is_night == "1",
          routeType = routeTypeMap.GetValueOrDefault(jsonResponse.features[i].properties.route_type, RouteType.Other)
        };
      }
      return geoData;
    }
  }
}
