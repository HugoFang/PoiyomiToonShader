// Material/Shader Inspector for Unity 2017/2018
// Copyright (C) 2019 Thryrallo

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Thry;

public class ThryEditor : ShaderGUI
{
    public const string EXTRA_OPTIONS_PREFIX = "--";
    public const float MATERIAL_NOT_RESET = 69.12f;

    public const string PROPERTY_NAME_USING_THRY_EDITOR = "shader_is_using_thry_editor";
    public const string PROPERTY_NAME_MASTER_LABEL = "shader_master_label";
    public const string PROPERTY_NAME_PRESETS_FILE = "shader_presets";
    public const string PROPERTY_NAME_LABEL_FILE = "shader_properties_label_file";
    public const string PROPERTY_NAME_LOCALE = "shader_properties_locale";

    private static Texture2D settingsTexture;

    // Stores the different shader properties
    private ShaderHeader shaderparts;

    // UI Instance Variables
	private PresetHandler presetHandler;
    private int customRenderQueueFieldInput = -1;

    // shader specified values
    private string masterLabelText = null;
    private List<ButtonData> footer;

    // sates
    private static bool reloadNextDraw = false;
    private bool firstOnGUICall = true;
    private bool wasUsed = false;

    public struct InputEvent
    {
        public bool HadMouseDownRepaint;
        public bool HadMouseDown;
        public bool MouseClick;

        public bool is_alt_down;

        public bool is_drag_drop_event;
        public bool is_drop_event;

        public Vector2 mouse_position;
    }

    public static InputEvent input = new InputEvent();
    // Contains Editor Data
    private EditorData current;
    public static EditorData currentlyDrawing;

    public abstract class ShaderPart
    {
        public int xOffset = 0;
        public GUIContent content;
        public System.Object property_data = null;
        public PropertyOptions options;
        public bool reference_properties_exist = false;

        public ShaderPart(int xOffset, string displayName, PropertyOptions options)
        {
            this.xOffset = xOffset;
            this.options = options;
            this.content = new GUIContent(displayName, options.hover);
            this.reference_properties_exist = options.reference_properties != null && options.reference_properties.Length > 0;
        }

        public abstract void DrawInternal();

        public void Draw()
        {
            bool is_enabled = DrawingData.is_enabled;
            if (options.condition_enable != null && is_enabled)
            {
                DrawingData.is_enabled = options.condition_enable.Test();
                EditorGUI.BeginDisabledGroup(!DrawingData.is_enabled);
            }
            if (options.condition_show.Test())
            {
                DrawInternal();
                testAltClick(DrawingData.lastGuiObjectHeaderRect, this);
            }
            if (options.condition_enable != null && is_enabled)
            {
                DrawingData.is_enabled = true;
                EditorGUI.EndDisabledGroup();
            }
        }
    }

    public class ShaderGroup : ShaderPart
    {
        public List<ShaderPart> parts = new List<ShaderPart>();

        public ShaderGroup() : base(0, "", new PropertyOptions())
        {

        }

        public ShaderGroup(PropertyOptions options) : base(0, "", new PropertyOptions())
        {
            this.options = options;
        }

        public ShaderGroup(MaterialProperty prop, MaterialEditor materialEditor, string displayName, int xOffset, PropertyOptions options) : base(xOffset, displayName, options)
        {
            
        }

        public void addPart(ShaderPart part)
        {
            parts.Add(part);
        }

        public override void DrawInternal()
        {
            foreach (ShaderPart part in parts)
            {
                part.Draw();
            }
        }
    }

    public class ShaderHeader : ShaderGroup
    {
        public ThryEditorHeader guiElement;

        public ShaderHeader() : base()
        {

        }

        public ShaderHeader(MaterialProperty prop, MaterialEditor materialEditor, string displayName, int xOffset, PropertyOptions options) : base(prop, materialEditor, displayName, xOffset, options)
        {
            this.guiElement = new ThryEditorHeader(materialEditor, prop.name);
        }

        public override void DrawInternal()
        {
            
            currentlyDrawing.currentProperty = this;
            guiElement.Foldout(xOffset, content, currentlyDrawing.gui);
            Rect headerRect = DrawingData.lastGuiObjectHeaderRect;
            if (guiElement.getState())
            {
                EditorGUILayout.Space();
                foreach (ShaderPart part in parts)
                {
                    part.Draw();
                }
                EditorGUILayout.Space();
            }
            DrawingData.lastGuiObjectHeaderRect = headerRect;
        }
    }

    public class ShaderProperty : ShaderPart
    {
        public MaterialProperty materialProperty;
        public bool drawDefault;

        public float setFloat;
        public bool updateFloat;

        public bool forceOneLine = false;

        public ShaderProperty(MaterialProperty materialProperty, string displayName, int xOffset, PropertyOptions options, bool forceOneLine) : base(xOffset, displayName, options)
        {
            this.materialProperty = materialProperty;
            drawDefault = false;
            this.forceOneLine = forceOneLine;
        }

        public override void DrawInternal()
        {
            PreDraw();
            currentlyDrawing.currentProperty = this;
            DrawingData.lastGuiObjectHeaderRect = new Rect(-1,-1,-1,-1);
            int oldIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = xOffset + 1;

            if (drawDefault)
                DrawDefault();
            else if (forceOneLine)
                currentlyDrawing.editor.ShaderProperty(GUILayoutUtility.GetRect(content, Styles.Get().vectorPropertyStyle), this.materialProperty, this.content);
            else
                currentlyDrawing.editor.ShaderProperty(this.materialProperty, this.content);

            EditorGUI.indentLevel = oldIndentLevel;
            if (DrawingData.lastGuiObjectHeaderRect.x==-1) DrawingData.lastGuiObjectHeaderRect = GUILayoutUtility.GetLastRect();
        }

        public virtual void PreDraw() { }

        public virtual void DrawDefault() { }
    }

    public class InstancingProperty : ShaderProperty
    {
        public InstancingProperty(MaterialProperty materialProperty, string displayName, int xOffset, PropertyOptions options, bool forceOneLine) : base(materialProperty, displayName, xOffset,options, forceOneLine)
        {
            drawDefault = true;
        }

        public override void DrawDefault()
        {
            currentlyDrawing.editor.EnableInstancingField();
        }
    }
    public class GIProperty : ShaderProperty
    {
        public GIProperty(MaterialProperty materialProperty, string displayName, int xOffset, PropertyOptions options, bool forceOneLine) : base(materialProperty, displayName, xOffset,options, forceOneLine)
        {
            drawDefault = true;
        }

        public override void DrawDefault()
        {
            currentlyDrawing.editor.LightmapEmissionFlagsProperty(xOffset, true);
        }
    }
    public class DSGIProperty : ShaderProperty
    {
        public DSGIProperty(MaterialProperty materialProperty, string displayName, int xOffset, PropertyOptions options, bool forceOneLine) : base(materialProperty, displayName, xOffset,options, forceOneLine)
        {
            drawDefault = true;
        }

        public override void DrawDefault()
        {
            currentlyDrawing.editor.DoubleSidedGIField();
        }
    }
    public class LocaleProperty : ShaderProperty
    {
        public LocaleProperty(MaterialProperty materialProperty, string displayName, int xOffset, PropertyOptions options, bool forceOneLine) : base(materialProperty, displayName, xOffset,options, forceOneLine)
        {
            drawDefault = true;
        }

        public override void DrawDefault()
        {
            GuiHelper.DrawLocaleSelection(this.content,currentlyDrawing.gui.locale.available_locales, currentlyDrawing.gui.locale.selected_locale_index);
        }
    }

    public class TextureProperty : ShaderProperty
    {
        public bool showFoldoutProperties = false;
        public bool hasFoldoutProperties = false;
        public bool hasScaleOffset = false;

        public TextureProperty(MaterialProperty materialProperty, string displayName, int xOffset, PropertyOptions options, bool hasScaleOffset, bool forceThryUI) : base(materialProperty, displayName, xOffset, options, false)
        {
            drawDefault = forceThryUI;
            this.hasScaleOffset = hasScaleOffset;
            this.hasFoldoutProperties = hasScaleOffset || reference_properties_exist;
        }

        public override void PreDraw()
        {
            DrawingData.currentTexProperty = this;
        }

        public override void DrawDefault()
        {
            Rect pos = GUILayoutUtility.GetRect(content, Styles.Get().vectorPropertyStyle);
            GuiHelper.drawConfigTextureProperty(pos, materialProperty, content, currentlyDrawing.editor, hasFoldoutProperties);
            DrawingData.lastGuiObjectHeaderRect = pos;
        }
    }

    //-------------Init functions--------------------

    private Dictionary<string, string> LoadDisplayNamesFromFile()
    {
        //load display names from file if it exists
        MaterialProperty label_file_property = null;
        foreach (MaterialProperty m in current.properties) if (m.name == PROPERTY_NAME_LABEL_FILE) label_file_property = m;
        Dictionary<string, string> labels = new Dictionary<string, string>();
        if (label_file_property != null)
        {
            string[] guids = AssetDatabase.FindAssets(label_file_property.displayName);
            if (guids.Length == 0)
            {
                Debug.LogWarning("Label File could not be found");
                return labels;
            }
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            string[] data = Regex.Split(Thry.FileHelper.ReadFileIntoString(path), @"\r?\n");
            foreach (string d in data)
            {
                string[] set = Regex.Split(d, ":=");
                if (set.Length > 1) labels[set[0]] = set[1];
            }
        }
        return labels;
    }

    private PropertyOptions ExtractExtraOptionsFromDisplayName(ref string displayName)
    {
        if (displayName.Contains("--"))
        {
            string[] parts = displayName.Split(new string[] { EXTRA_OPTIONS_PREFIX }, 2, System.StringSplitOptions.None);
            displayName = parts[0];
            PropertyOptions options = Parser.ParseToObject<PropertyOptions>(parts[1]);
            if(options!=null)
                return options;
        }
        return new PropertyOptions();
    }

    private enum ThryPropertyType
    {
        none,property,master_label, footer,header,header_end,header_start,group_start,group_end,instancing,dsgi,lightmap_flags,locale,space
    }

    private ThryPropertyType GetPropertyType(MaterialProperty p)
    {
        string name = p.name;
        MaterialProperty.PropFlags flags = p.flags;
        if (name.StartsWith("footer_") && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.footer;
        if (name.StartsWith("m_end") && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.header_end;
        if (name.StartsWith("m_start") && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.header_start;
        if (name.StartsWith("m_") && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.header;
        if (name.StartsWith("g_start") && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.group_start;
        if (name.StartsWith("g_end") && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.group_end;
        if (Regex.Match(name.ToLower(), @"^space\d*$").Success)
            return ThryPropertyType.space;
        if (name == PROPERTY_NAME_MASTER_LABEL)
            return ThryPropertyType.master_label;
        if (name.Replace(" ","") == "Instancing" && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.instancing;
        if (name.Replace(" ", "") == "DSGI" && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.dsgi;
        if (name.Replace(" ", "") == "LightmapFlags" && flags == MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.lightmap_flags;
        if (name.Replace(" ", "") == PROPERTY_NAME_LOCALE)
            return ThryPropertyType.locale;
        if (flags != MaterialProperty.PropFlags.HideInInspector)
            return ThryPropertyType.property;
        return ThryPropertyType.none;
    }

    private Locale locale;

    private void LoadLocales()
    {
        MaterialProperty locales_property = null;
        locale = null;
        foreach (MaterialProperty m in current.properties) if (m.name == PROPERTY_NAME_LOCALE) locales_property = m;
        if (locales_property != null)
        {
            string displayName = locales_property.displayName;
            PropertyOptions options = ExtractExtraOptionsFromDisplayName(ref displayName);
            locale = new Locale(options.file_name);
            locale.selected_locale_index = (int)locales_property.floatValue;
        }
    }

    //finds all properties and headers and stores them in correct order
    private void CollectAllProperties()
	{
        //load display names from file if it exists
        MaterialProperty[] props = current.properties;
        Dictionary<string, string> labels = LoadDisplayNamesFromFile();
        LoadLocales();

        current.propertyDictionary = new Dictionary<string, ShaderProperty>();
        shaderparts = new ShaderHeader(); //init top object that all Shader Objects are childs of
		Stack<ShaderGroup> headerStack = new Stack<ShaderGroup>(); //header stack. used to keep track if current header to parent new objects to
		headerStack.Push(shaderparts); //add top object as top object to stack
		headerStack.Push(shaderparts); //add top object a second time, because it get's popped with first actual header item
		footer = new List<ButtonData>(); //init footer list
		int headerCount = 0;
		for (int i = 0; i < props.Length; i++)
		{
            string displayName = props[i].displayName;
            if (locale != null)
                foreach (string key in locale.GetAllKeys())
                    displayName = displayName.Replace("locale::" + key, locale.Get(key));
            displayName = Regex.Replace(displayName, @"''", "\"");
            
            if (labels.ContainsKey(props[i].name)) displayName = labels[props[i].name];
            PropertyOptions options = ExtractExtraOptionsFromDisplayName(ref displayName);

            int offset = options.offset + headerCount;

            ThryPropertyType type = GetPropertyType(props[i]);
            switch (type)
            {
                case ThryPropertyType.header:
                    headerStack.Pop();
                    break;
                case ThryPropertyType.header_start:
                    offset = options.offset + ++headerCount;
                    break;
                case ThryPropertyType.header_end:
                    headerStack.Pop();
                    headerCount--;
                    break;
            }
            ShaderProperty newPorperty = null;
            switch (type)
            {
                case ThryPropertyType.master_label:
                    masterLabelText = displayName;
                    break;
                case ThryPropertyType.footer:
                    footer.Add(Parser.ParseToObject<ButtonData>(displayName));
                    break;
                case ThryPropertyType.header:
                case ThryPropertyType.header_start:
                    ShaderHeader newHeader = new ShaderHeader(props[i], current.editor, displayName, offset, options);
                    headerStack.Peek().addPart(newHeader);
                    headerStack.Push(newHeader);
                    break;
                case ThryPropertyType.group_start:
                    ShaderGroup new_group = new ShaderGroup(options);
                    headerStack.Peek().addPart(new_group);
                    headerStack.Push(new_group);
                    break;
                case ThryPropertyType.group_end:
                    headerStack.Pop();
                    break;
                case ThryPropertyType.none:
                case ThryPropertyType.property:
                    DrawingData.lastPropertyUsedCustomDrawer = false;
                    current.editor.GetPropertyHeight(props[i]);
                    bool forceOneLine = props[i].type == MaterialProperty.PropType.Vector && !DrawingData.lastPropertyUsedCustomDrawer;
                    if (props[i].type == MaterialProperty.PropType.Texture)
                        newPorperty = new TextureProperty(props[i], displayName, offset, options, props[i].flags != MaterialProperty.PropFlags.NoScaleOffset ,!DrawingData.lastPropertyUsedCustomDrawer);
                    else
                        newPorperty = new ShaderProperty(props[i], displayName, offset, options, forceOneLine);
                    break;
                case ThryPropertyType.lightmap_flags:
                    newPorperty = new GIProperty(props[i], displayName, offset, options, false);
                    break;
                case ThryPropertyType.dsgi:
                    newPorperty = new DSGIProperty(props[i], displayName, offset, options, false);
                    break;
                case ThryPropertyType.instancing:
                    newPorperty = new InstancingProperty(props[i], displayName, offset, options, false);
                    break;
                case ThryPropertyType.locale:
                    newPorperty = new LocaleProperty(props[i], displayName, offset, options, false);
                    break;
            }
            if (newPorperty != null)
            {
                current.propertyDictionary.Add(props[i].name, newPorperty);
                if (type != ThryPropertyType.none)
                    headerStack.Peek().addPart(newPorperty);
            }
		}
	}

    //-------------Functions------------------

    public void UpdateRenderQueueInstance()
    {
        if (current.materials != null) foreach (Material m in current.materials)
            if (m.shader.renderQueue != m.renderQueue)
                Thry.MaterialHelper.UpdateRenderQueue(m, current.defaultShader);
    }

    //-------------Draw Functions----------------

    private static void testAltClick(Rect rect, ShaderPart property)
    {
        if (input.HadMouseDownRepaint && input.is_alt_down && rect.Contains(input.mouse_position))
        {
            if (property.options.altClick != null)
                property.options.altClick.Perform();
        }
    }

    public void OnOpen()
    {
        Config config = Config.Get();

        //get material targets
        Object[] targets = current.editor.targets;
        current.materials = new Material[targets.Length];
        for (int i = 0; i < targets.Length; i++) current.materials[i] = targets[i] as Material;

        //collect shader properties
        CollectAllProperties();

        presetHandler = new PresetHandler(current.properties);

        //init settings texture
        if (settingsTexture == null)
        {
            byte[] fileData = File.ReadAllBytes(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("thry_settings_icon")[0]));
            settingsTexture = new Texture2D(2, 2);
            settingsTexture.LoadImage(fileData);
        }

        current.shader = current.materials[0].shader;
        string defaultShaderName = current.materials[0].shader.name.Split(new string[] { "-queue" }, System.StringSplitOptions.None)[0].Replace(".differentQueues/", "");
        current.defaultShader = Shader.Find(defaultShaderName);

        //update render queue if render queue selection is deactivated
        if (!config.renderQueueShaders && !config.showRenderQueue)
        {
            current.materials[0].renderQueue = current.defaultShader.renderQueue;
            UpdateRenderQueueInstance();
        }

        foreach (MaterialProperty p in current.properties) if (p.name == PROPERTY_NAME_USING_THRY_EDITOR) p.floatValue = MATERIAL_NOT_RESET;

        if (current.materials != null) foreach (Material m in current.materials) ShaderImportFixer.backupSingleMaterial(m);
        firstOnGUICall = false;
    }

    public override void OnClosed(Material  material)
    {
        base.OnClosed(material);
        if (current.materials != null) foreach (Material m in current.materials) ShaderImportFixer.backupSingleMaterial(m);
        firstOnGUICall = true;
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        base.AssignNewShaderToMaterial(material, oldShader, newShader);
        firstOnGUICall = true;
    }

    private void UpdateEvents()
    {
        Event e = Event.current;
        input.MouseClick = e.type == EventType.MouseDown;
        if (input.MouseClick) input.HadMouseDown = true;
        if (input.HadMouseDown && e.type == EventType.Repaint) input.HadMouseDownRepaint = true;
        input.is_alt_down = e.alt;
        input.mouse_position = e.mousePosition;
        input.is_drop_event = e.type == EventType.DragPerform;
        input.is_drag_drop_event = input.is_drop_event || e.type == EventType.DragUpdated;
    }

    //-------------Main Function--------------
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
	{
        if (firstOnGUICall || (reloadNextDraw && Event.current.type == EventType.Layout))
        {
            current = new EditorData();
            current.editor = materialEditor;
            current.gui = this;
            current.properties = props;
            current.textureArrayProperties = new List<ShaderProperty>();
            current.firstCall = true;
        }

        //handle events
        UpdateEvents();

        //first time call inits
        if (firstOnGUICall || (reloadNextDraw && Event.current.type == EventType.Layout)) OnOpen();

        currentlyDrawing = current;

        //sync shader and get preset handler
        Config config = Config.Get();
        if(current.materials!=null)
            Mediator.SetActiveShader(current.materials[0].shader, presetHandler: presetHandler);


        //editor settings button + shader name + presets
        EditorGUILayout.BeginHorizontal();
        //draw editor settings button
        if (GUILayout.Button(new GUIContent(" Thry Editor",settingsTexture), EditorStyles.largeLabel ,new GUILayoutOption[] { GUILayout.MaxHeight(20) })) {
            Settings window = Settings.getInstance();
            window.Show();
            window.Focus();
        }
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

        //draw master label if exists
        if (masterLabelText != null) GuiHelper.DrawMasterLabel(masterLabelText, GUILayoutUtility.GetLastRect().y);
        //draw presets if exists

        presetHandler.drawPresets(current.properties, current.materials);
		EditorGUILayout.EndHorizontal();

		//shader properties
		foreach (ShaderPart part in shaderparts.parts)
		{
            part.Draw();
		}

        //Render Queue selection
        if (config.showRenderQueue)
        {
            if (config.renderQueueShaders)
            {
                customRenderQueueFieldInput = GuiHelper.drawRenderQueueSelector(current.defaultShader, customRenderQueueFieldInput);
                EditorGUILayout.LabelField("Default: " + current.defaultShader.name);
                EditorGUILayout.LabelField("Shader: " + current.shader.name);
            }
            else
            {
                materialEditor.RenderQueueField();
            }
        }

        //footer
        GuiHelper.drawFooters(footer);

        Event e = Event.current;
        bool isUndo = (e.type == EventType.ExecuteCommand || e.type == EventType.ValidateCommand) && e.commandName == "UndoRedoPerformed";
        if (reloadNextDraw && Event.current.type==EventType.Layout) reloadNextDraw = false;
        if (isUndo) reloadNextDraw = true;

        //test if material has been reset
        if (wasUsed && e.type == EventType.Repaint)
        {
            foreach (MaterialProperty p in props)
                if (p.name == PROPERTY_NAME_USING_THRY_EDITOR && p.floatValue != MATERIAL_NOT_RESET)
                {
                    reloadNextDraw = true;
                    break;
                }
            wasUsed = false;
        }

        if (e.type == EventType.Used) wasUsed = true;
        if (config.showRenderQueue && config.renderQueueShaders) UpdateRenderQueueInstance();
        if (input.HadMouseDownRepaint) input.HadMouseDown = false;
        input.HadMouseDownRepaint = false;
        current.firstCall = false;
    }

    public static void reload()
    {
        reloadNextDraw = true;
    }

    public static void loadValuesFromMaterial()
    {
        if (currentlyDrawing.editor != null)
        {
            try
            {
                Material m = ((Material)currentlyDrawing.editor.target);
                foreach (MaterialProperty property in currentlyDrawing.properties)
                {
                    switch (property.type)
                    {
                        case MaterialProperty.PropType.Float:
                        case MaterialProperty.PropType.Range:
                            property.floatValue = m.GetFloat(property.name);
                            break;
                        case MaterialProperty.PropType.Texture:
                            property.textureValue = m.GetTexture(property.name);
                            break;
                        case MaterialProperty.PropType.Color:
                            property.colorValue = m.GetColor(property.name);
                            break;
                        case MaterialProperty.PropType.Vector:
                            property.vectorValue = m.GetVector(property.name);
                            break;
                    }

                }
            }
            catch (System.Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
    }

    public static void propertiesChanged()
    {
        if (currentlyDrawing.editor != null)
        {
            try
            {
                currentlyDrawing.editor.PropertiesChanged();
            }
            catch (System.Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
    }

    public static void addUndo(string label)
    {
        if (currentlyDrawing.editor != null)
        {
            try
            {
                currentlyDrawing.editor.RegisterPropertyChangeUndo(label);
            }
            catch (System.Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
    }

    public static void repaint()
    {
        if (currentlyDrawing.editor != null)
        {
            try
            {
                currentlyDrawing.editor.Repaint();
            }
            catch(System.Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
    }

    private static string edtior_directory_path;
    public static string GetThryEditorDirectoryPath()
    {
        if (edtior_directory_path == null)
        {
            string[] guids = AssetDatabase.FindAssets("ThryEditor");
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith("/ThryEditor.cs"))
                {
                    edtior_directory_path = p.GetDirectoryPath().RemoveOneDirectory();
                    break;
                }
            }
        }
        return edtior_directory_path;
    }

    //----------Static Helper Functions

    //finds a property in props by name, if it doesnt exist return null
    public static MaterialProperty FindProperty(MaterialProperty[] props, string name)
	{
		MaterialProperty ret = null;
		foreach (MaterialProperty p in props)
		{
			if (p.name == name) { ret = p; }
		}
		return ret;
	}
}
