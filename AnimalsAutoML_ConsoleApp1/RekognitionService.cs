using Amazon.Rekognition;
using Amazon.Rekognition.Model;
namespace AnimalsAutoML_ConsoleApp1;

public class RekognitionService(IAmazonRekognition client)
{
    public async Task<float?> CompareFacesAsync(byte[] firstImageBytes, byte[] secondImageBytes,
        CancellationToken cancellationToken = default)
    {
        using var source = new MemoryStream(firstImageBytes);
        using var target = new MemoryStream(secondImageBytes);
        var response = await client.CompareFacesAsync(new CompareFacesRequest
        {
            SourceImage = new Image { Bytes = source },
            TargetImage = new Image { Bytes = target },
            SimilarityThreshold = 0f
        }, cancellationToken);
        return response.FaceMatches?.OrderByDescending(match => match.Similarity).FirstOrDefault()?.Similarity;
    }
}

