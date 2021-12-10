#nullable enable

using System;
using System.Numerics;

using MyUtilities;

using static System.Console;

// path of the model file
var filepath = "ModelData/Octahedron.txt";

// read mesh from the file
var mesh = SearchMesh.CreateFromTxtFile(filepath);

// apply loop subdivision algorithm many times (optional)
// do not do this for ConvexHullOfBunny or ConvexHullOfElephantRotated model
// as it yields a non-convex polyhedron
mesh = mesh.CreateSubdividedMesh();
mesh = mesh.CreateSubdividedMesh();
mesh = mesh.CreateSubdividedMesh();

// create a simulator instance
var simulator = new IntervalWavefront.Simulator();

// specify the source point
var rand = new Random(0);
var face = mesh.Faces[rand.Next() % mesh.Faces.Length];
double a = rand.NextDouble(), b = rand.NextDouble(), c = rand.NextDouble();
var pos = (a * face.Edges[0].Tail.Position + b * face.Edges[1].Tail.Position + c * face.Edges[2].Tail.Position) / (a + b + c);

// initialize the simulator
simulator.Initialize(mesh, face, pos);

// run the algorithm (you can specify the maximum number of steps)
simulator.SearchStep(numSteps: int.MaxValue);

// write the number of steps and current radius to the console
WriteLine(simulator.StepCount);
WriteLine(simulator.Radius);

// write down the source unfolding to a SVG file
simulator.RootSegment!.ComputeUnfolding().WriteTo("unfolding.svg", simulator.Radius);

// all code below is to write down a perspective-projected view to a SVG file

// get the cut locus as lines
// here we distinguish the "global" cut locus
// as the area surrounded by the two geodesics contains a face
LineBuffer localCutLocusBuffer = new(), globalCutLocusBuffer = new();
simulator.RidgeSink?.AddToBuffer(localCutLocusBuffer, globalCutLocusBuffer);

// center of the canvas (canvas size becomes 800x600)
var center = new Vector2(400, 300);

float phi = MathF.PI / 8;  // longitudinal rotation radians
float theta = MathF.PI / 8; // latitudinal rotation radians
var rotationMatrix = Matrix4x4.CreateRotationY(phi) * Matrix4x4.CreateRotationX(theta);

// compute a projection matrix
var projection = new Projection {
	Aspect = center.X / center.Y, // aspect ratio
	BoundY = 1.25f,               // maximum visible Y in the xz-plane
	ConvergenceZ = 0,             // stereographic convergence plane
};
float eyeZ = 10;
var projectionMatrix = projection.CalcMatrix(eyeX: 0, eyeZ: eyeZ);

// create a transformation
var transformation = new Transformation {
	ViewProjMatrix = mesh.ModelMatrix * rotationMatrix * projectionMatrix,
	Center = center,
};

// create a svg renderer
using var renderer = new SvgRenderer(
	filename: "view.svg",
	transformation: transformation,
	lineWidth: 2
);

// compute the viewpoint and update IsFrontFacing for each face
var inverseViewMatrix = Matrix4x4.Transpose(rotationMatrix) * mesh.InverseModelMatrix;
var eye = (DVector3)Vector3.Transform(new Vector3(0, 0, eyeZ), inverseViewMatrix);
mesh.UpdateIsFrontFacing(eye);

// draw back-facing part (dimmed)
{
	renderer.SetColor(0x7F7F7F, isLineGroup: true);
	mesh.Draw(renderer, drawFront: false);

	renderer.SetColor(0x003F00, isLineGroup: true);
	localCutLocusBuffer.Draw(renderer, drawFront: false);
	globalCutLocusBuffer.Draw(renderer, drawFront: false);
}

// draw mesh
renderer.SetColor(0xFFFFFF, isLineGroup: true);
mesh.Draw(renderer, drawFront: true);

// draw cut locus
renderer.SetColor(0x00BF00, isLineGroup: true);
localCutLocusBuffer.Draw(renderer, drawFront: true);

renderer.SetColor(0x00FF00, isLineGroup: true);
globalCutLocusBuffer.Draw(renderer, drawFront: true);

// draw the farthest point as a yellow circle (not dimmed if back-facing)
renderer.SetColor(0xFFFF00, isLineGroup: false);
if (simulator.RidgeSink != null) {
	DVector3 position = simulator.RidgeSink.Position;
	renderer.DrawSprite((Vector3)position, SpriteType.Circle);
}

// draw the source point as a yellow cross sign (not dimmed if back-facing)
renderer.DrawSprite((Vector3)pos, SpriteType.Cross);
