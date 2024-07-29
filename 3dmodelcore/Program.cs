string filepath = null;
int axis = -1;
int numsplits = -1;
int samplerate = -1;
int sliceAudioLength = -1;
string outpath = null;
bool confirm = true;
bool homogenize = true;

string[] arguments = string.Join(" ", args).Split(" -").Where((string str) => !string.IsNullOrWhiteSpace(str)).ToArray();
foreach(string arg in arguments)
{
    string argTrim = arg.Trim().Trim('-');

    if (argTrim == "noconfirm") { confirm = false; continue; }
    if (argTrim == "nosmooth") { homogenize = false; continue; }

    switch (argTrim[0])
    {
        case 'i':
            filepath = argTrim[2..];
            break;
        case 'a':
            axis = int.Parse(argTrim[2..]) - 1;
            break;
        case 'n':
            numsplits = int.Parse(argTrim[2..]);
            break;
        case 's':
            samplerate = int.Parse(argTrim[2..]);
            break;
        case 'l':
            sliceAudioLength = int.Parse(argTrim[2..]);
            break;
        case 'o':
            outpath = argTrim[2..];
            break;
        default:
            errorTerminate(argTrim + " is an invalid argument");
            break;
    }
}

if (filepath == null)
{
    Console.Write("filepath (.obj model): ");
    filepath = Console.ReadLine() ?? "";
    Console.WriteLine();
}

string[] fileContent = File.ReadAllLines(filepath ?? "");

if (axis == -1)
{
    Console.Write("axis (1,2,3): ");
    axis = int.Parse(Console.ReadLine() ?? "") - 1;
    Console.WriteLine();
}
if (axis < 0 || axis > 2) errorTerminate("bombshoes. it's over (invalid axis)");

if (numsplits == -1)
{
    Console.Write("number of splits: ");
    numsplits = int.Parse(Console.ReadLine() ?? "");
    Console.WriteLine();
}
if (numsplits < 1) errorTerminate("why (invalid number of splits)");

if (samplerate == -1)
{
    Console.Write("samplerate: ");
    samplerate = int.Parse(Console.ReadLine() ?? "");
    Console.WriteLine();
}
if (samplerate < 1) errorTerminate("why (invalid samplerate");

if (sliceAudioLength == -1)
{
    Console.Write("samples per slice: ");
    sliceAudioLength = int.Parse(Console.ReadLine() ?? "");
    Console.WriteLine();
}
if (sliceAudioLength < 1) errorTerminate("why (invalid split length)");

if (outpath == null)
{
    Console.Write("output filepath: ");
    outpath = Console.ReadLine() ?? "";
    Console.WriteLine();
}

FileStream outfile = File.Create(outpath);




int xaxis = (axis + 1) % 3;
int yaxis = (axis + 2) % 3;

const float epsilon = 0.00001f;

List<float[]> vertices = new List<float[]>();
List<int[]> triangles = new List<int[]>();

float minOnAxis = float.PositiveInfinity;
float maxOnAxis = float.NegativeInfinity;

float minXAxis = float.PositiveInfinity;
float maxXAxis = float.NegativeInfinity;
float minYAxis = float.PositiveInfinity;
float maxYAxis = float.NegativeInfinity;

//parse file for vertices and triangles and collect data
for (int i = 0; i < fileContent.Length; i++)
{
    if (fileContent[i].Length < 2) continue;
    if (fileContent[i][0] == 'v' && fileContent[i][1] == ' ')
    {
        string[] lineParts = fileContent[i].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        float[] vertexCoords = new float[] { float.Parse(lineParts[1]), float.Parse(lineParts[2]), float.Parse(lineParts[3]) };
        vertices.Add(vertexCoords);

        minOnAxis = vertexCoords[axis] < minOnAxis ? vertexCoords[axis] : minOnAxis;
        maxOnAxis = vertexCoords[axis] > maxOnAxis ? vertexCoords[axis] : maxOnAxis;

        minXAxis = vertexCoords[xaxis] < minXAxis ? vertexCoords[xaxis] : minXAxis;
        maxXAxis = vertexCoords[xaxis] > maxXAxis ? vertexCoords[xaxis] : maxXAxis;
        minYAxis = vertexCoords[yaxis] < minYAxis ? vertexCoords[yaxis] : minYAxis;
        maxYAxis = vertexCoords[yaxis] > maxYAxis ? vertexCoords[yaxis] : maxYAxis;
    }

    if (fileContent[i][0] == 'f' && fileContent[i][1] == ' ')
    {
        string[] lineParts = fileContent[i].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int tri = 0; tri < lineParts.Length - 3; tri++)
        {
            int[] triangle = new int[] { parseVertexIndex(lineParts[1]), parseVertexIndex(lineParts[2 + tri]), parseVertexIndex(lineParts[3 + tri]) };
            triangles.Add(triangle);
        }
    }
}

Console.WriteLine("parsed file");
Console.WriteLine($"{vertices.Count} vertices, {triangles.Count} triangles");

float dist = (maxOnAxis - minOnAxis) / numsplits;
float position = minOnAxis + dist / 2;

Split[] splits = new Split[numsplits];

for (int splitnum = 0; splitnum < numsplits; splitnum++)
{
    Split split = new();

    for (int i = 0; i < triangles.Count; i++)
    {
        int[] tri = triangles[i];
        int firstPoint = -1;

        for (int v = 0; v < 3; v++)
        {
            float[] vertexA = vertices[tri[v]];
            float[] vertexB = vertices[tri[(v + 1) % 3]];

            float div = vertexA[axis] - vertexB[axis];
            if (MathF.Abs(div) < epsilon) continue;
            float p = (vertexA[axis] - position) / div;
            if (p < 0 || p > 1) continue;

            float[] point = new float[2] { lerp(vertexA[xaxis], vertexB[xaxis], p), lerp(vertexA[yaxis], vertexB[yaxis], p) };

            bool pointExists = false;
            int pointIndex = -1;

            for (int compareIndex = 0; compareIndex < split.points.Count; compareIndex++)
            {
                if (closeEnough(point, split.points[compareIndex], epsilon))
                {
                    pointExists = true;
                    pointIndex = compareIndex;
                    break;
                }
            }
            if (!pointExists)
            {
                pointIndex = split.points.Count;
                split.points.Add(point);
            }

            if (firstPoint == -1)
            {
                firstPoint = pointIndex;
            }
            else
            {
                Line newLine = new(pointIndex, firstPoint);
                if (newLine.a == newLine.b) continue;
                if (!split.lines.Contains(newLine))
                    split.lines.Add(newLine);
            }
        }
    }
    rewriteProgress(((float)splitnum + 1) / numsplits, $"splitting {splitnum + 1}/{numsplits}");

    splits[splitnum] = split;

    position += dist;
}

Console.WriteLine();

//switch lines to chains then chainify
for (int splitIndex = 0; splitIndex < splits.Length; splitIndex++)
{
    for (int i = 0; i < splits[splitIndex].lines.Count; i++)
    {
        splits[splitIndex].chains.Add(new Chain(splits[splitIndex].lines[i]));
    }

    bool changed = true;
    while (changed)
    {
        changed = false;
        for (int i = 0; i < splits[splitIndex].chains.Count; i++)
        {
            //append congruent
            for (int j = splits[splitIndex].chains.Count - 1; j > i; j--)
            {
                //this is a mess. the ones indexed by i are thisChain, by j are otherChain
                if (splits[splitIndex].chains[i].nodes.First() == splits[splitIndex].chains[j].nodes.Last())
                {
                    splits[splitIndex].chains[i].nodes.Reverse();
                    splits[splitIndex].chains[j].nodes.Reverse();
                    splits[splitIndex].chains[j].nodes.RemoveAt(0);
                    splits[splitIndex].chains[i].nodes.AddRange(splits[splitIndex].chains[j].nodes);
                    splits[splitIndex].chains.RemoveAt(j);

                    changed = true;
                    continue;
                }
                if (splits[splitIndex].chains[i].nodes.First() == splits[splitIndex].chains[j].nodes.First())
                {
                    splits[splitIndex].chains[j].nodes.RemoveAt(0);
                    splits[splitIndex].chains[i].nodes.Reverse();
                    splits[splitIndex].chains[i].nodes.AddRange(splits[splitIndex].chains[j].nodes);
                    splits[splitIndex].chains.RemoveAt(j);

                    changed = true;
                    continue;
                }

                if (splits[splitIndex].chains[i].nodes.Last() == splits[splitIndex].chains[j].nodes.Last())
                {
                    splits[splitIndex].chains[j].nodes.RemoveAt(splits[splitIndex].chains[j].nodes.Count - 1);
                    splits[splitIndex].chains[j].nodes.Reverse();
                    splits[splitIndex].chains[i].nodes.AddRange(splits[splitIndex].chains[j].nodes);
                    splits[splitIndex].chains.RemoveAt(j);

                    changed = true;
                    continue;
                }
                if (splits[splitIndex].chains[i].nodes.Last() == splits[splitIndex].chains[j].nodes.First())
                {
                    splits[splitIndex].chains[j].nodes.RemoveAt(0);
                    splits[splitIndex].chains[i].nodes.AddRange(splits[splitIndex].chains[j].nodes);
                    splits[splitIndex].chains.RemoveAt(j);

                    changed = true;
                    continue;
                }
            }
        }
    }

    //group into one chain
    while (splits[splitIndex].chains.Count > 1)
    {
        float minDistance = float.PositiveInfinity;
        int closestInd = 0; //chain[n] is closest
        bool thisIsFirst = false; //whether chain[0]'s first element is the closest element
        bool otherIsFirst = false; //whether chain[n]'s first element is the closest element

        int thisFirst = splits[splitIndex].chains[0].nodes.First();
        int thisLast = splits[splitIndex].chains[0].nodes.Last();
        int otherFirst;
        int otherLast;
        float thisDist;
        for (int i = 1; i < splits[splitIndex].chains.Count; i++)
        {
            otherFirst = splits[splitIndex].chains[0].nodes.First();
            otherLast = splits[splitIndex].chains[0].nodes.Last();

            thisDist = distance(splits[splitIndex].points[thisFirst], splits[splitIndex].points[otherFirst]);
            if (thisDist < minDistance)
            {
                minDistance = thisDist;
                thisIsFirst = true;
                otherIsFirst = true;
                closestInd = i;
            }
            thisDist = distance(splits[splitIndex].points[thisFirst], splits[splitIndex].points[otherLast]);
            if (thisDist < minDistance)
            {
                minDistance = thisDist;
                thisIsFirst = true;
                otherIsFirst = false;
                closestInd = i;
            }
            thisDist = distance(splits[splitIndex].points[thisLast], splits[splitIndex].points[otherFirst]);
            if (thisDist < minDistance)
            {
                minDistance = thisDist;
                thisIsFirst = false;
                otherIsFirst = true;
                closestInd = i;
            }
            thisDist = distance(splits[splitIndex].points[thisLast], splits[splitIndex].points[otherLast]);
            if (thisDist < minDistance)
            {
                minDistance = thisDist;
                thisIsFirst = false;
                otherIsFirst = false;
                closestInd = i;
            }
        }

        if (thisIsFirst) splits[splitIndex].chains[0].nodes.Reverse();
        if (!otherIsFirst) splits[splitIndex].chains[closestInd].nodes.Reverse();
        splits[splitIndex].chains[0].nodes.AddRange(splits[splitIndex].chains[closestInd].nodes);
        splits[splitIndex].chains.RemoveAt(closestInd);
    }

    rewriteProgress(((float)splitIndex + 1) / numsplits, $"optimized {splitIndex + 1}/{numsplits}");

    if (splits[splitIndex].chains.Count == 0) continue;
    float length = 0;
    for (int j = 0; j < splits[splitIndex].chains[0].nodes.Count - 1; j++)
    {
        length += distance(splits[splitIndex].points[splits[splitIndex].chains[0].nodes[j]], splits[splitIndex].points[splits[splitIndex].chains[0].nodes[j + 1]]);
    }
    splits[splitIndex].chains[0].length = length;
}

Console.WriteLine();

if (homogenize)
{
    for (int splitIndex = 0; splitIndex < splits.Length; splitIndex++)
    {
        splits[splitIndex].HomogenizeChains();

        rewriteProgress(((float)splitIndex + 1) / numsplits, $"homogenized {splitIndex + 1}/{numsplits}");
    }
    Console.WriteLine();
}

//audio header

BinaryWriter writer = new BinaryWriter(outfile);
writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
writer.Write(36 + 4 * (sliceAudioLength * numsplits));
writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
writer.Write(16);
writer.Write((ushort)1);
writer.Write((ushort)2);
writer.Write(samplerate);
writer.Write(samplerate * 4);
writer.Write((ushort)4);
writer.Write((ushort)16);
writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
writer.Write(4 * (sliceAudioLength * numsplits));

for (int splitInd = 0; splitInd < splits.Length; splitInd++)
{
    for (int samplesIn = 0; samplesIn < sliceAudioLength; samplesIn++)
    {
        if (splits[splitInd].chains.Count == 0)
        {
            writer.Write((short)0);
            writer.Write((short)0);
            continue;
        }
        float d = ((float)samplesIn / sliceAudioLength) * splits[splitInd].chains[0].length;
        float[] coordinate = pointAtDistOnChain(d, splits[splitInd], 0);
        short leftChannel = (short)map(coordinate[0], minXAxis, maxXAxis, short.MinValue, short.MaxValue);
        short rightChannel = (short)map(coordinate[1], minYAxis, maxYAxis, short.MinValue, short.MaxValue);
        writer.Write(leftChannel);
        writer.Write(rightChannel);
    }
    rewriteProgress(((float)splitInd + 1) / numsplits, $"wrote {splitInd + 1}/{numsplits}");
}

writer.Close();
outfile.Close();

Console.Write("\nfinished :).");
if (confirm)
{
    Console.WriteLine(" press enter to end it all");
    Console.ReadLine();
}
else
{
    Console.WriteLine();
}


static int parseVertexIndex(string indexStr)
{
    return int.Parse(indexStr.Substring(0, indexStr.IndexOf('/'))) - 1;
}

static float lerp(float a, float b, float t)
{
    return a + (b - a) * t;
}

static bool closeEnough(float[] a, float[] b, float eps)
{
    return MathF.Abs(a[0] - b[0]) < eps && MathF.Abs(a[1] - b[1]) < eps;
}

static float map(float t, float start, float end, float toStart, float toEnd)
{
    //if (t < start || t > end) Console.WriteLine("invalid t");
    return ((t - start) / (end - start)) * (toEnd - toStart) + toStart;
}

static void errorTerminate(string message)
{
    Console.WriteLine(message);
    Console.WriteLine("press enter to terminate");
    Console.ReadLine();
    System.Environment.Exit(1);
}

/*
static int[] distributeChainSamples(List<Chain> chains, int total)
{
    int[] distribution = new int[chains.Count];
    float[] fractionalDistribution = new float[chains.Count];
    float totalLength = 0;
    foreach (Chain chain in chains) totalLength += chain.length;

    int totalSoFar = 0;
    for (int i = 0; i < distribution.Length; i++)
    {
        float thisSize = (chains[i].length / totalLength) * total;
        totalSoFar += (int)MathF.Floor(thisSize);
        fractionalDistribution[i] = thisSize % 1;
        distribution[i] = (int)MathF.Floor(thisSize);
    }

    for (int i = 0; i < total-totalSoFar; i++)
    {
        float max = 0;
        int ind = -1;
        for (int j = 0; j < fractionalDistribution.Length; j++)
        {
            if (fractionalDistribution[j] > max)
            {
                max = fractionalDistribution[j];
                ind = j;
            }
        }
        fractionalDistribution[ind] = 0f;
        distribution[ind]++;
    }

    return distribution;
}
*/

static float distance(float[] a, float[] b)
{
    return MathF.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
}

static float[] pointAtDistOnChain(float dist, Split split, int chainIndex)
{
    if (dist == split.chains[chainIndex].length)
    {
        return split.points[split.chains[chainIndex].nodes[^1]];
    }

    float distRemaining = dist;
    float linkLength = distance(split.points[split.chains[chainIndex].nodes[0]], split.points[split.chains[chainIndex].nodes[1]]);
    int i = 1;
    while (distRemaining > linkLength)
    {
        if (i > split.chains[chainIndex].nodes.Count - 2)
        {
            //Console.WriteLine("invalid input dist (not user error)");
            break;
        }
        distRemaining -= linkLength;
        linkLength = distance(split.points[split.chains[chainIndex].nodes[i]], split.points[split.chains[chainIndex].nodes[i + 1]]);
        i++;
    }
    i--;

    return new float[2]
    {
        lerp(split.points[split.chains[chainIndex].nodes[i]][0], split.points[split.chains[chainIndex].nodes[i+1]][0], distRemaining/linkLength),
        lerp(split.points[split.chains[chainIndex].nodes[i]][1], split.points[split.chains[chainIndex].nodes[i+1]][1], distRemaining/linkLength)
    };
}

static void rewriteProgress(float portion, string extraText, int sections = 20)
{
    string progress = "[";
    float portionRemaining = portion * sections;
    for (int i = 0; i < sections; i++)
    {
        if (portionRemaining - i >= 1)
        {
            progress += "█";
            continue;
        }
        if (portionRemaining - i >= 0.75)
        {
            progress += "▓";
            continue;
        }
        if (portionRemaining - i >= 0.5)
        {
            progress += "▒";
            continue;
        }
        if (portionRemaining - i >= 0.25)
        {
            progress += "░";
            continue;
        }
        progress += " ";
    }
    progress += "]";

    Console.Write("\r{0} {1}   ", progress, extraText);
}

struct Split
{
    public List<float[]> points = new List<float[]>();
    public List<Line> lines = new List<Line>();
    public List<Chain> chains = new List<Chain>();

    public Split(){}
    public void HomogenizeChains()
    {
        foreach (Chain chain in this.chains)
        {
            float minX = float.PositiveInfinity;
            int minInd = -1;
            for (int i = 0; i < chain.nodes.Count; i++)
            {
                if(this.points[chain.nodes[i]][0] < minX)
                {
                    minX = this.points[chain.nodes[i]][0];
                    minInd = i;
                }
            }

            bool left = false;
            if (this.points[chain.nodes[(minInd + 1) % chain.nodes.Count]][1] >
                    this.points[chain.nodes[(minInd - 1 + chain.nodes.Count) % chain.nodes.Count]][1]) left = true;

            bool loop = chain.nodes.First() == chain.nodes.Last();
            if (loop) chain.nodes.RemoveAt(chain.nodes.Count - 1);

            if (left)
            {
                minInd = chain.nodes.Count - minInd - 1;
                chain.nodes.Reverse();
            }
            
            List<int> A = chain.nodes.Skip(minInd).ToList();
            List<int> B = chain.nodes.SkipLast(chain.nodes.Count - minInd + 1).ToList();

            chain.nodes.Clear();
            chain.nodes.AddRange(A);
            chain.nodes.AddRange(B);
            if (loop)
            {
                chain.nodes.Add(chain.nodes.First());
            }
        }
    }
}

struct Line
{
    public int a;
    public int b;
    public Line(int a, int b)
    {
        this.a = a;
        this.b = b;
    }
    public static bool operator ==(Line a, Line b)
    {
        return (a.a == b.a && a.b == b.b) || (a.b == b.a && a.a == b.b);
    }

    public static bool operator !=(Line a, Line b)
    {
        return !(a==b);
    }
}

class Chain
{
    public List<int> nodes;
    public float length;
    public Chain(Line line)
    {
        this.nodes = new List<int> { line.a, line.b };
        this.length = 0;
    }
}