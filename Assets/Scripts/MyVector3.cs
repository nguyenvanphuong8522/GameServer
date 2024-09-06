public class MyVector3
{
    public RequestType type;
    public int id;
    public float x;
    public float y;
    public float z;

    public MyVector3(float x, float y, float z, RequestType type)
    {
        this.type = type;
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public MyVector3()
    {
        x = 0; y = 0; z = 0;
        type = RequestType.CREATE;
    }



}
