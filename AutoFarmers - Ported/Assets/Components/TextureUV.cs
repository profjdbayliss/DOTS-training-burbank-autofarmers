using Unity.Entities;


// Texture UVs for the tiles on the floor
public struct TextureUV : IComponentData
{
    public int nameID;
    public float pixelStartX;
    public float pixelStartY;
    public float pixelEndY;
    public float pixelEndX;

    public TextureUV(int name, float startX, float startY, float endX, float endY)
    {
        nameID = name;
        pixelStartX = startX;
        pixelStartY = startY;
        pixelEndX = endX;
        pixelEndY = endY;
    }
}


