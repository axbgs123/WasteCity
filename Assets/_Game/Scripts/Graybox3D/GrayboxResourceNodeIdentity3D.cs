namespace WasteCity.Graybox3D
{
    public static class GrayboxResourceNodeIdentity3D
    {
        public static string Create(int worldX, int worldY)
        {
            return $"world.resource-node.{worldX}.{worldY}";
        }
    }
}
