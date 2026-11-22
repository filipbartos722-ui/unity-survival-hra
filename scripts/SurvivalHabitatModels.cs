using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Shared low-poly models for a project with no art assigned. One mesh per silhouette.
internal sealed class SurvivalHabitatModels
{
    internal enum Form { Spruce, Birch, Acacia, Fern, Bloom, Reed, Aloe, Ruin, Gallery, Coal, Iron, Gold, Ruby, WaterCell }
    readonly Dictionary<Form,Mesh> models = new Dictionary<Form,Mesh>();
    readonly Dictionary<Texture,Material> grasses = new Dictionary<Texture,Material>();
    readonly Texture2D atlas;
    internal readonly Material solids, WaterMaterial, Precipitation;
    readonly SmoothTerrain owner;
    static readonly Color[] ink = {
        new Color(.29f,.20f,.13f), new Color(.70f,.68f,.54f), new Color(.10f,.28f,.22f), new Color(.20f,.43f,.29f),
        new Color(.39f,.59f,.28f), new Color(.56f,.68f,.34f), new Color(.48f,.58f,.33f), new Color(.92f,.71f,.30f),
        new Color(.65f,.49f,.72f), new Color(.41f,.44f,.43f), new Color(.54f,.57f,.51f), new Color(.21f,.24f,.25f),
        new Color(.15f,.17f,.19f), new Color(.75f,.39f,.20f), new Color(.91f,.70f,.24f), new Color(.72f,.17f,.29f)
    };
    internal static Material Lit(string label)
    {
        var rp=GraphicsSettings.currentRenderPipeline;
        Material template = rp!=null ? rp.defaultMaterial : null;
        Material m;
        if(template!=null && template.shader.isSupported) m=new Material(template);
        else
        {
            var shader=rp!=null ? rp.defaultShader : Shader.Find("Standard");
            if(shader==null || !shader.isSupported) shader=Resources.Load<Shader>("SurvivalWorld/PaletteFallback");
            m=new Material(shader);
        }
        m.name=label;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",.16f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness",.16f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color",Color.white);
        return m;
    }
    internal static void Albedo(Material m,Texture tex)
    {
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex",tex);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap",tex);
        if (m.HasProperty("_BaseColorMap")) m.SetTexture("_BaseColorMap",tex);
    }
    internal SurvivalHabitatModels(SmoothTerrain owner)
    {
        this.owner=owner;
        atlas=new Texture2D(ink.Length,1,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
        atlas.SetPixels(ink); atlas.Apply(false,true);
        solids=Lit("Les, kámen a rudy"); Albedo(solids,atlas);
        WaterMaterial=Effect("Water",owner.waterColor);
        Precipitation=Effect("Precipitation",Color.white);
    }
    static Material Effect(string shaderName,Color tint)
    {
        Shader shader=Resources.Load<Shader>("SurvivalWorld/"+shaderName);
        if (shader==null) { Debug.LogError("Chybí Resources/SurvivalWorld/"+shaderName+".shader"); return Lit(shaderName); }
        var m=new Material(shader) { name=shaderName };
        if(m.HasProperty("_Color")) m.SetColor("_Color",tint);
        return m;
    }
    internal Material Grass(Texture tex)
    {
        if(tex==null) tex=Texture2D.whiteTexture;
        if(grasses.TryGetValue(tex,out var m)) return m;
        m=Effect("Grass",owner.grassColor); Albedo(m,tex);
        if(m.HasProperty("_Bend")) m.SetFloat("_Bend",owner.grassBend);
        grasses.Add(tex,m); return m;
    }
    internal Mesh Get(Form form)
    {
        if(models.TryGetValue(form,out var mesh)) return mesh;
        var b=new Model();
        switch(form)
        {
            case Form.Spruce:
                b.Pole(new Vector3(0,-.04f,0),new Vector3(.07f,4.6f,0),.23f,.07f,0);
                for(int i=0;i<4;i++)
                {
                    float y=1.0f+i*.9f, r=1.45f-i*.25f;
                    b.Pole(new Vector3(.06f*i,y,0),new Vector3(.08f*i,y+1.9f-i*.13f,.12f),r,.03f,2+i%2,9,i*.43f);
                }
                break;
            case Form.Birch:
            case Form.Acacia:
                bool dry=form==Form.Acacia; int bark=dry?0:1;
                b.Pole(Vector3.zero,new Vector3(.13f,3.45f,.07f),.19f,.10f,bark,7);
                for(int i=0;i<5;i++)
                {
                    float a=i*2.4f, spread=dry?1.15f:.71f;
                    Vector3 end=new Vector3(Mathf.Cos(a)*spread,3.2f+i*.22f,Mathf.Sin(a)*spread);
                    b.Pole(new Vector3(.1f,2f+i*.16f,.05f),end,.11f,.04f,bark,6);
                    b.Crown(end,dry ? 1.23f : .90f,dry ? .65f : 1.48f,3+i%3,i*.7f);
                }
                break;
            case Form.Fern: case Form.Bloom: case Form.Aloe: case Form.Reed:
                for(int i=0;i<8;i++)
                {
                    float a=i*2.39996f, h=.30f+(i%3)*.14f;
                    Vector3 outwards=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    Vector3 tip=outwards*.30f+Vector3.up*h;
                    if(form==Form.Reed) tip=outwards*.20f+Vector3.up*(h+1f);
                    if(form==Form.Bloom || form==Form.Reed)
                    {
                        b.Pole(outwards*.08f,tip,.019f,.012f,4,4);
                        b.Crown(tip,.065f,form==Form.Reed ? .21f : .09f,form==Form.Reed?0:(i%3==0?8:7),a);
                    }
                    b.Leaf(outwards,form==Form.Aloe ? .16f : .10f,form==Form.Fern ? .60f : .33f,h,4+i%3);
                }
                break;
            case Form.Ruin:
                for(int k=0;k<4;k++)
                {
                    b.Block(new Vector3(-.98f+.025f*k,.32f+k*.55f,0),new Vector3(.64f,.55f,.70f),9+k%2,k*.035f);
                    b.Block(new Vector3(.96f-.02f*k,.31f+k*.55f,.08f),new Vector3(.65f,.55f,.70f),9+(k+1)%2,-k*.03f);
                }
                b.Block(new Vector3(.03f,2.48f,.04f),new Vector3(2.68f,.45f,.78f),10,.018f);
                b.Block(new Vector3(1.55f,.22f,.75f),new Vector3(.82f,.43f,.57f),9,.48f);
                b.Block(new Vector3(-.72f,.11f,.54f),new Vector3(.55f,.22f,.48f),6,.14f);
                break;
            case Form.Gallery:
                for(int side=-1;side<=1;side+=2)
                {
                    b.Pole(new Vector3(side*.99f,-.06f,0),new Vector3(side*.80f,2.25f,0),.17f,.14f,0,7);
                    b.Pole(new Vector3(side*.85f,1.64f,0),new Vector3(side*.26f,2.24f,0),.10f,.09f,1,6);
                    b.Block(new Vector3(side*.96f,.13f,0),new Vector3(.50f,.30f,.5f),9,.10f);
                }
                b.Block(new Vector3(0,2.33f,0),new Vector3(2.27f,.29f,.35f),0,0);
                break;
            case Form.WaterCell:
                b.Block(Vector3.one*.5f,new Vector3(1f,1f,1f),10,0);
                break;
            default:
                int ore=(int)form-(int)Form.Coal;
                b.Pole(Vector3.zero,new Vector3(.01f,.20f,0),.47f,.37f,11,9,.28f);
                for(int i=0;i<6;i++)
                {
                    float a=i*2.4f; Vector3 p=new Vector3(Mathf.Cos(a)*.24f,.12f,Mathf.Sin(a)*.24f);
                    Vector3 q=p+new Vector3(p.x*.46f,.2f+(i%3)*.10f,p.z*.47f);
                    b.Pole(p,q,.11f+i%2*.038f,.085f,12+ore,5,a);
                    b.Pole(q,q+Vector3.up*.12f,.085f,.003f,12+ore,5,a);
                }
                break;
        }
        mesh=b.data.Upload(form.ToString()); models.Add(form,mesh); return mesh;
    }
    internal void Dispose()
    {
        foreach(var m in models.Values) Object.Destroy(m);
        foreach(var m in grasses.Values) Object.Destroy(m);
        Object.Destroy(atlas); Object.Destroy(solids); Object.Destroy(WaterMaterial); Object.Destroy(Precipitation);
    }
    sealed class Model
    {
        internal readonly SurvivalMeshData data=new SurvivalMeshData();
        void Tri(Vector3 a,Vector3 b,Vector3 c,int tint)
        {
            int i=data.vertices.Count; Vector3 n=Vector3.Cross(b-a,c-a).normalized;
            data.vertices.Add(a); data.vertices.Add(b); data.vertices.Add(c);
            Vector2 uv=new Vector2((tint+.5f)/ink.Length,.5f);
            for(int j=0;j<3;j++) { data.normals.Add(n); data.uv.Add(uv); data.triangles.Add(i+j); }
        }
        internal void Pole(Vector3 a,Vector3 b,float ra,float rb,int tint,int sides=7,float twist=0f)
        {
            Vector3 axis=(b-a).normalized;
            Vector3 right=Vector3.Cross(axis,Mathf.Abs(axis.z)<.9f?Vector3.forward:Vector3.right).normalized;
            Vector3 front=Vector3.Cross(right,axis);
            for(int i=0;i<sides;i++)
            {
                float t=twist+i*2f*Mathf.PI/sides, u=twist+(i+1)*2f*Mathf.PI/sides;
                Vector3 v=right*Mathf.Cos(t)+front*Mathf.Sin(t), w=right*Mathf.Cos(u)+front*Mathf.Sin(u);
                Vector3 p=a+ra*v, q=a+ra*w, r=b+rb*v, s=b+rb*w;
                Tri(p,r,q,tint); Tri(q,r,s,tint); Tri(a,p,q,tint); Tri(b,s,r,tint);
            }
        }
        internal void Crown(Vector3 p,float r,float h,int color,float a)
        {
            Vector3 mid=p+Vector3.up*(h*.33f);
            Pole(p-Vector3.up*h*.22f,mid,r*.35f,r,color,7,a);
            Pole(mid,p+Vector3.up*h,r,r*.18f,color,7,a);
        }
        internal void Leaf(Vector3 outward,float width,float len,float h,int color)
        {
            Vector3 side=Vector3.Cross(Vector3.up,outward)*width;
            Vector3 mid=outward*(len*.48f)+Vector3.up*h*.72f, tip=outward*len+Vector3.up*h;
            Tri(Vector3.zero,mid+side,tip,color); Tri(Vector3.zero,tip,mid-side,color);
            Tri(Vector3.zero,tip,mid+side,color); Tri(Vector3.zero,mid-side,tip,color);
        }
        internal void Block(Vector3 centre,Vector3 size,int color,float angle)
        {
            var v=new Vector3[8]; Quaternion q=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
            for(int i=0;i<8;i++) v[i]=centre+q*Vector3.Scale(size,new Vector3((i&1)-.5f,((i>>1)&1)-.5f,((i>>2)&1)-.5f));
            int[] ids={0,4,6,2, 1,3,7,5, 0,1,5,4, 2,6,7,3, 0,2,3,1, 4,5,7,6};
            for(int i=0;i<24;i+=4) { Tri(v[ids[i]],v[ids[i+1]],v[ids[i+2]],color); Tri(v[ids[i]],v[ids[i+2]],v[ids[i+3]],color); }
        }
    }
}
