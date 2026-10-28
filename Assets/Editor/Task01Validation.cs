using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CardGame.Core;
using CardGame.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Run with Unity -batchmode -executeMethod Task01Validation.Run. No webcam is opened.</summary>
public static class Task01Validation
{
    private static int _checks;
    // Independent fixture: OpenCV first-rotation byte pairs, NOT read from production Codes.
    private static readonly byte[,] Official = { {181,50}, {15,154}, {51,45}, {153,70}, {84,158},
        {121,205}, {158,46}, {196,242}, {254,218}, {207,86} };
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new Exception(message);
    }

    [MenuItem("CardGame/Validate Task 01 (no webcam)")]
    public static void Run()
    {
        try
        {
            if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before running validation.");
            RunRegressionChecks();
            string report = $"PASS: {_checks} assertions. Official IDs 0-9; rotations; perspective; negative images; all 10 project card PNGs; card assets; scene references; duel integration (TASK 02 replaces legacy direct player damage).\nNo physical webcam or Play Mode test performed.\n";
            File.WriteAllText("Task01-test-results.txt", report);
            Debug.Log(report);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("Task01-test-results.txt", $"FAIL after {_checks} checks: {error}\n");
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    public static int RunRegressionChecks()
    {
        _checks = 0;
        DetectorTests();
        CardArtworkTests();
        SceneAndGameplayTests();
        return _checks;
    }

    private static void DetectorTests()
    {
        var expected = new Dictionary<ushort, int>();
        for (int id = 0; id < 10; id++)
        {
            ushort bits = (ushort)((Official[id, 0] << 8) | Official[id, 1]);
            for (int r = 0; r < 4; r++)
            {
                Check(!expected.ContainsKey(bits), "Official code collision");
                expected.Add(bits, id);
                // Fixture rotation uses a separate array transpose, not the detector rotation.
                var rotated = new bool[4,4];
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++) rotated[x, 3-y] = ((bits >> (15-y*4-x)) & 1) != 0;
                bits = 0;
                foreach (bool bit in rotated) bits = (ushort)((bits << 1) | (bit ? 1 : 0));
            }
        }
        for (int bits = 0; bits <= ushort.MaxValue; bits++)
            Check(ArucoDetector.Match((ushort)bits) == (expected.TryGetValue((ushort)bits, out int id) ? id : -1),
                "Non-exact or ambiguous pattern accepted: " + bits);

        var detector = new ArucoDetector();
        var results = new List<ArucoDetector.Detection>();
        float[] angles = { 0, 15, 45, 89, 90, 135, 180, 270 };
        for (int id = 0; id < 10; id++)
            foreach (float angle in angles)
                for (int perspective = 0; perspective < 2; perspective++)
                {
                    byte[] image = Render((ushort)((Official[id, 0] << 8) | Official[id, 1]), angle, perspective != 0, false, id+17);
                    detector.Detect(image, 320, 240, 31, 8, 200, 0.5f, results);
                    Check(results.Count == 1 && results[0].ID == id,
                        $"Detection failed ID {id}, angle {angle}, perspective {perspective}; found {Describe(results)}");
                }
        var random = new System.Random(4317);
        for (int i = 0; i < 80; i++)
        {
            ushort bits;
            do { bits = (ushort)random.Next(65536); } while (expected.ContainsKey(bits));
            detector.Detect(Render(bits, i % 45, false, false, i), 320, 240, 31, 8, 200, 0.5f, results);
            Check(results.Count == 0, "Random non-dictionary grid accepted");
        }
        for (int i = 0; i < 10; i++)
        {
            byte[] image = new byte[320*240];
            for (int p = 0; p < image.Length; p++) image[p] = (byte)(random.Next(2)*255);
            detector.Detect(image, 320, 240, 31, 8, 200, 0.5f, results);
            Check(results.Count == 0, "Random black/white image accepted");
        }
        detector.Detect(Render(0xB532, 0, false, true, 0), 320, 240, 31, 8, 200, 0.5f, results);
        Check(results.Count == 0, "Broken border accepted");
        detector.Detect(Render(0xF991, 0, false, false, 0), 320, 240, 31, 8, 200, 0.5f, results);
        Check(results.Count == 0, "Unsupported OpenCV ID 10 accepted");
        detector.Detect(new byte[320*240], 320, 240, 31, 8, 200, 0.5f, results);
        Check(results.Count == 0, "Solid black accepted");
        byte[] left = Render(0xB532, 15, true, false, 12);
        foreach (ushort rightCode in new ushort[] { 0x0F9A, 0xB532 })
        {
            byte[] right = Render(rightCode, 45, false, false, 13);
            byte[] pair = new byte[640*240];
            for (int row=0; row<240; row++)
            {
                Array.Copy(left,row*320,pair,row*640,320);
                Array.Copy(right,row*320,pair,row*640+320,320);
            }
            detector.Detect(pair, 640, 240, 31, 8, 200, 0.5f, results);
            Check(results.Count == 2, "Multiple physical markers were collapsed into one scan");
        }
    }

    private static string Describe(List<ArucoDetector.Detection> detections)
    {
        string text = "";
        foreach (var d in detections) text += d.ID + " ";
        return text;
    }

    // Independent fixture renderer: solve image->unit-square projective coefficients by elimination.
    private static byte[] Render(ushort bits, float angle, bool perspective, bool brokenBorder, int seed)
    {
        double radians = angle*Math.PI/180, cos = Math.Cos(radians), sin = Math.Sin(radians);
        double[,] corners = { {-55,-55}, {55,-55}, {55,55}, {-55,55} };
        if (perspective) { corners[0,0] += 12; corners[1,1] += 8; corners[2,0] -= 7; corners[3,1] -= 6; }
        var matrix = new double[8,9];
        for (int i = 0; i < 4; i++)
        {
            double x = 160+corners[i,0]*cos-corners[i,1]*sin;
            double y = 120+corners[i,0]*sin+corners[i,1]*cos;
            double u = i == 1 || i == 2 ? 1 : 0, v = i >= 2 ? 1 : 0;
            int a = i*2, b = a+1;
            matrix[a,0]=x; matrix[a,1]=y; matrix[a,2]=1; matrix[a,6]=-u*x; matrix[a,7]=-u*y; matrix[a,8]=u;
            matrix[b,3]=x; matrix[b,4]=y; matrix[b,5]=1; matrix[b,6]=-v*x; matrix[b,7]=-v*y; matrix[b,8]=v;
        }
        for (int col=0; col<8; col++)
        {
            int pivot=col;
            for(int row=col+1;row<8;row++) if(Math.Abs(matrix[row,col])>Math.Abs(matrix[pivot,col])) pivot=row;
            for(int j=col;j<9;j++) { double temp=matrix[col,j]; matrix[col,j]=matrix[pivot,j]; matrix[pivot,j]=temp; }
            double divisor=matrix[col,col];
            for(int j=col;j<9;j++) matrix[col,j]/=divisor;
            for(int row=0;row<8;row++)
            {
                if(row==col)continue;
                double factor=matrix[row,col];
                for(int j=col;j<9;j++) matrix[row,j]-=factor*matrix[col,j];
            }
        }
        var image = new byte[320*240];
        var noise = new System.Random(seed);
        for(int y=0;y<240;y++) for(int x=0;x<320;x++)
        {
            double d=matrix[6,8]*x+matrix[7,8]*y+1;
            double u=(matrix[0,8]*x+matrix[1,8]*y+matrix[2,8])/d;
            double v=(matrix[3,8]*x+matrix[4,8]*y+matrix[5,8])/d;
            bool white=true;
            if(u>=0 && u<1 && v>=0 && v<1)
            {
                int cx=(int)(u*6),cy=(int)(v*6);
                white = cx>0 && cy>0 && cx<5 && cy<5 && ((bits>>(15-(cy-1)*4-(cx-1)))&1)!=0;
                if(brokenBorder && cy==0)white=true;
            }
            int value=(white?210:20)+x/12+noise.Next(-5,6);
            image[y*320+x]=(byte)Math.Max(0,Math.Min(255,value));
        }
        return image;
    }

    private static void CardArtworkTests()
    {
        string[] files = Directory.GetFiles("Assets/Cards", "*.png");
        Array.Sort(files, StringComparer.Ordinal);
        Check(files.Length == 10, "Expected ten existing card PNGs");
        var detector = new ArucoDetector();
        var results = new List<ArucoDetector.Detection>();
        for (int id = 0; id < files.Length; id++)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Check(texture.LoadImage(File.ReadAllBytes(files[id])), "Cannot load card image " + files[id]);
                Color32[] pixels = texture.GetPixels32();
                int w = 640, h = Mathf.RoundToInt((float)w * texture.height / texture.width);
                byte[] gray = new byte[w*h];
                for (int y=0; y<h; y++) for (int x=0; x<w; x++)
                {
                    Color32 p = pixels[(texture.height-1-y*texture.height/h)*texture.width+x*texture.width/w];
                    gray[y*w+x]=(byte)((p.r*77+p.g*150+p.b*29)>>8);
                }
                detector.Detect(gray,w,h,31,8,200,0.5f,results);
                Check(results.Count==1 && results[0].ID==id,
                    "Project card artwork detection failed " + files[id] + "; found " + Describe(results));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }

    private static void SceneAndGameplayTests()
    {
        // Preview scene preserves the user's open scene and unsaved Inspector edits.
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SampleScene.unity");
        GameManager gm = null;
        WebCamArucoManager camera = null;
        CardDatabaseManager db = null;
        var oldGame = GameManager.Instance;
        var oldDatabase = CardDatabaseManager.Instance;
        try
        {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (gm == null) gm = root.GetComponentInChildren<GameManager>();
            if (camera == null) camera = root.GetComponentInChildren<WebCamArucoManager>();
            if (db == null) db = root.GetComponentInChildren<CardDatabaseManager>();
        }
        Check(gm != null && camera != null && db != null, "Scene manager component missing");
        Check(new SerializedObject(gm).FindProperty("arucoManager").objectReferenceValue == camera, "Camera reference missing");
        Check(new SerializedObject(camera).FindProperty("displayImage").objectReferenceValue != null, "Webcam RawImage missing");
        // Direct initialization avoids opening the webcam or running interactive Play Mode.
        typeof(CardDatabaseManager).GetProperty("Instance").SetValue(null, db);
        typeof(GameManager).GetProperty("Instance").SetValue(null, gm);
        Invoke(db, "LoadCards");
        Invoke(gm, "InitGame");
        Check(db.CardCount==10,"Expected exactly 10 CardData assets");
        string[] names={"Kızıl Öfke","Buz Ejderi","Gölge Suikastçısı","Şimşek Şövalyesi","Orman Muhafızı","Ay Büyücüsü","Demir Golem","Kum Fırtınası","Anka Savaşçısı","Kurbağa Bilgesi"};
        int[] hp={120,95,70,110,130,75,160,90,100,80},atk={35,55,65,45,30,60,25,50,55,40};
        for(int i=0;i<10;i++)
        {
            CardData card=db.GetCardByID(i);
            Check(card!=null && card.cardName==names[i] && card.health==hp[i] && card.attackPower==atk[i],"Card data mismatch "+i);
            Check(!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(card)),"Card is not an asset "+i);
        }
        Check(gm.State==DuelState.WaitingForPlayer1Card,"Duel initial state incorrect");
        Check(gm.GetPlayerHP(0)==0 && gm.GetPlayerHP(1)==0,"Legacy player HP still active");
        Check(gm.HasUsedCard(0)==false,"Initial used-card state incorrect");
        Check(gm.OwnsCard(0,0) && !gm.OwnsCard(1,0),"ID 0 ownership incorrect");
        }
        finally
        {
            typeof(GameManager).GetProperty("Instance").SetValue(null, oldGame);
            typeof(CardDatabaseManager).GetProperty("Instance").SetValue(null, oldDatabase);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static void Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
}
