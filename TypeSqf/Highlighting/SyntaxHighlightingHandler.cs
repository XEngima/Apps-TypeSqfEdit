using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TypeSqf.Model;

namespace TypeSqf.Edit.Highlighting
{
    public static class SyntaxHighlightingHandler
    {
        private static string XshdContentSqx
        {
            get
            {
                return @"
<?xml version=""1.0""?>
<SyntaxDefinition name=""Sqf"" extensions="".sqf"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <!-- The named colors 'Comment' and 'String' are used in SharpDevelop to detect if a line is inside a multiline string/comment -->
  <Color name=""Default"" exampleText=""get; private set;"" />
  <Color name=""Comment"" foreground=""#%CommentsForeColor%"" fontWeight=""%CommentsBold%"" fontStyle=""%CommentsItalic%"" exampleText=""// comment"" />
  <Color name=""String"" foreground=""#%StringsForeColor%"" fontWeight=""%StringsBold%"" fontStyle=""%StringsItalic%"" exampleText=""string text = &quot;Hello, World!&quot;""/>
  <Color name=""PropertyGetSet"" foreground=""#%PropertyGetSetForeColor%"" fontWeight=""%PropertyGetSetBold%"" fontStyle=""%PropertyGetSetItalic%"" exampleText=""get; private set;"" />
  <Color name=""ClassItems"" foreground=""#%ClassKeywordForeColor%"" fontWeight=""%ClassKeywordBold%"" fontStyle=""%ClassKeywordItalic%"" exampleText=""namespace Engima"" />
  <Color name=""PrivateFields"" foreground=""#%ClassKeywordForeColor%"" fontWeight=""%ClassKeywordBold%"" fontStyle=""%ClassKeywordItalic%"" exampleText=""private []""/>
  <Color name=""Brackets"" foreground=""#%BracketsForeColor%"" fontWeight=""%BracketsBold%"" fontStyle=""%BracketsItalic%"" exampleText=""([{}])"" />
  <Color name=""NativeTypes"" foreground=""#%TypesForeColor%"" fontWeight=""%TypesBold%"" fontStyle=""%TypesItalic%"" exampleText=""namespace Engima"" />
  <Color name=""PrivateVariable"" foreground=""#%PrivateVariableForeColor%"" fontWeight=""%PrivateVariableBold%"" fontStyle=""%PrivateVariableItalic%"" exampleText=""_privateVariable""/>
  <Color name=""MemberNames"" foreground=""#%ClassMemberNamesForeColor%"" fontWeight=""%ClassMemberNamesBold%"" fontStyle=""%ClassMemberNamesItalic%"" exampleText=""public property Count""/>
  <Color name=""Keywords"" foreground=""#%KeywordsForeColor%"" fontWeight=""%KeywordsBold%"" fontStyle=""%KeywordsItalic%"" exampleText=""if (a) then {} else {}""/>
  <Color name=""Operators"" foreground=""#%OperatorsForeColor%"" fontWeight=""%OperatorsBold%"" fontStyle=""%OperatorsItalic%"" exampleText=""== !="" />
  <Color name=""GlobalFunctionHeader"" foreground=""#%GlobalFunctionHeaderForeColor%"" fontWeight=""%GlobalFunctionHeaderBold%"" fontStyle=""%GlobalFunctionHeaderItalic%"" exampleText=""o.ToString();""/>
  <Color name=""Preprocessor"" foreground=""#%PreprocessorForeColor%"" fontWeight=""%PreprocessorBold%"" fontStyle=""%PreprocessorItalic%"" exampleText=""#region Title"" />
  <Color name=""NumberLiterals"" foreground=""#%NumberLiteralsForeColor%"" fontWeight=""%NumberLiteralsBold%"" fontStyle=""%NumberLiteralsItalic%"" exampleText=""3.1415f""/>
  <Color name=""ScopeAndBreakCommands"" foreground=""#%ScopeAndBreakCommandsForeColor%"" fontWeight=""%ScopeAndBreakCommandsBold%"" fontStyle=""%ScopeAndBreakCommandsItalic%"" exampleText=""continue; return null;""/>
  <Color name=""TryCatchKeywords"" foreground=""#%KeywordsForeColor%"" fontWeight=""%KeywordsBold%"" fontStyle=""%KeywordsItalic%"" exampleText=""try {} catch {} finally {}""/>
  <Color name=""ValueLiterals"" foreground=""#%ValueLiteralsForeColor%"" fontWeight=""%ValueLiteralsBold%"" fontStyle=""%ValueLiteralsItalic%"" exampleText=""b = false; a = true;"" />

  <Property name=""DocCommentMarker"" value=""///"" />

  <RuleSet name=""CommentMarkerSet"">
    <Keywords fontWeight=""bold"" foreground=""Red"">
      <Word>TODO</Word>
      <Word>FIXME</Word>
    </Keywords>
    <Keywords fontWeight=""bold"" foreground=""#E0E000"">
      <Word>HACK</Word>
      <Word>UNDONE</Word>
    </Keywords>
  </RuleSet>

  <!-- This is the main ruleset. -->
  <RuleSet ignoreCase=""true"">
    
    <!--<Span color=""Preprocessor"">
      <Begin>\#</Begin>
      <RuleSet name=""PreprocessorSet"">
        <Span>
          --><!-- preprocessor directives that allows comments --><!--
          <Begin fontWeight=""bold"">
            (undef|if|elif|else|endif|line)\b
          </Begin>
          <RuleSet>
            <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
              <Begin>//</Begin>
            </Span>
          </RuleSet>
        </Span>
        <Span>
          --><!-- preprocessor directives that don't allow comments --><!--
          <Begin fontWeight=""bold"">
            (region|endregion|error|warning|pragma)\b
          </Begin>
        </Span>
      </RuleSet>
    </Span>-->

    <!--<Span color=""Comment"">
      <Begin color=""XmlDoc/DocComment"">///</Begin>
      <RuleSet>
        <Import ruleSet=""XmlDoc/DocCommentSet""/>
        <Import ruleSet=""CommentMarkerSet""/>
      </RuleSet>
    </Span>-->

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
      <Begin>//</Begin>
    </Span>

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"" multiline=""true"">
      <Begin>/\*</Begin>
      <End>\*/</End>
    </Span>

    <Rule color=""Default"">
      (?&lt;=\.)
      [A-Za-z0-9_]+
    </Rule>

    <Rule color=""Default"">
      (?&lt;=\#region\s+)
      [A-Za-z0-9_]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=public\s+class\s+)
      [A-Za-z0-9_]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=namespace\s+)
      [A-Za-z0-9_.]+
    </Rule>

    <Rule color=""Default"">
      private\s+(class|constructor|property)
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=public\s+class\s+[A-Za-z0-9_]+\s*:\s*)
      [^{]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=public\s+class\s+[A-Za-z0-9_]+\s*:\s*)
      .+
      (?=\{) # followed by ""{""
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=public\s+interface\s+)
      [A-Za-z0-9_]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=public\s+enum\s+)
      [A-Za-z0-9_]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=(method|property)\s+)
      [A-Za-z0-9._]+
      (?=\s+[A-Za-z0-9_]+) # followed by ""{""
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=private\s+)
      [A-Za-z0-9._]+
      (?=\s+[A-Za-z0-9_]+) # followed by an identifier
    </Rule>

    <Rule color=""ClassItems"">
      private\s+(static\s+)?fields
    </Rule>

    <Rule color=""ClassItems"">
      (?&lt;=\b)
      (method|property)
      (?=\b) # followed by ""[""
    </Rule>

    <Rule color=""Keywords"">
      (private|params)
      (?=\s*\[) # followed by ""[""
    </Rule>

    <Rule color=""MemberNames"">
      (?&lt;=property\s+[A-Za-z0-9._]+\s+)
      [A-Za-z0-9._]+
    </Rule>

    <Rule color=""MemberNames"">
      (?&lt;=property\s+)
      [A-Za-z0-9._]+
    </Rule>

    <Rule color=""MemberNames"">
      (?&lt;=method\s+[A-Za-z0-9._]+\s+)
      [A-Za-z0-9._]+
    </Rule>

    <Rule color=""MemberNames"">
      (?&lt;=method\s+)
      [A-Za-z0-9._]+
    </Rule>

    <Rule color=""MemberNames"">
      (?&lt;=(namespace)\s+)
      [A-Za-z0-9._]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=(\bas|\bnew|\bis)\s+)
      [A-Za-z0-9._]+
    </Rule>

    <Rule color=""PropertyGetSet"">
      (get;|set;|private\s+set;|protected\s+set;)
    </Rule>

    <Rule color=""ClassItems"">
      (protected|private|public)\s+(static\s+)?method
    </Rule>

    <Rule color=""ClassItems"">
      (protected|public)\s+((static|virtual|override)\s+)?method
    </Rule>

    <Rule color=""ClassItems"">
      (public|protected)\s+(static\s+)?property
    </Rule>

    <Rule color=""ClassItems"">
      (public)\s+(class|constructor|interface|enum)
    </Rule>

    <Span color=""String"" multiline=""true"">
      <Begin>""</Begin>
      <End>""</End>
      <RuleSet>
        <!-- span for escape sequences -->
        <Span begin=""\\"" end="".""/>
      </RuleSet>
    </Span>

    <Keywords color=""ValueLiterals"">
      <Word>true</Word>
      <Word>false</Word>
      <Word>east</Word>
      <Word>west</Word>
      <Word>blufor</Word>
      <Word>opfor</Word>
      <Word>civilian</Word>
      <Word>resistance</Word>
      <Word>independent</Word>
      <Word>sideEmpty</Word>
      <Word>sideAmbientLife</Word>
      <Word>sideLogic</Word>
      <Word>objNull</Word>
      <Word>controlNull</Word>
      <Word>displayNull</Word>
      <Word>grpNull</Word>
      <Word>locationNull</Word>
      <Word>taskNull</Word>
      <Word>scriptNull</Word>
      <Word>configNull</Word>
      <Word>classNull</Word>
      <Word>nil</Word>
    </Keywords>

    <Keywords color=""Keywords"">
      <Word>private</Word>
      <Word>var</Word>
      <Word>new</Word>
      <Word>if</Word>
      <Word>then</Word>
      <Word>exitWith</Word>
      <Word>else</Word>
      <Word>switch</Word>
      <Word>do</Word>
      <Word>case</Word>
      <Word>default</Word>
      <Word>for</Word>
      <Word>forEach</Word>
      <Word>in</Word>
      <Word>while</Word>
      <Word>as</Word>
      <Word>is</Word>
      <Word>return</Word>
    </Keywords>

    <Keywords color=""ClassItems"">
      <Word>using</Word>
      <Word>namespace</Word>
      <Word>enum</Word>
    </Keywords>

    <Keywords color=""ScopeAndBreakCommands"">
      <Word>scopeName</Word>
      <Word>breakTo</Word>
      <Word>breakOut</Word>
    </Keywords>

    <Keywords color=""TryCatchKeywords"">
      <Word>try</Word>
      <Word>catch</Word>
    </Keywords>

    <Rule color=""PrivateVariable"">
      (?&lt;![\d\w])_[_a-zA-Z0-9]+ # private variable
    </Rule>
    
    <Rule color=""Brackets"">
      [\{\[\(\)\]\}] # brackets
    </Rule>

    <Rule color=""Operators"">
      (==|!=|&lt;=?|&gt;=?|\!|&amp;&amp;|\|\|) # operators
    </Rule>

    <!-- Mark previous rule-->
    <Rule color=""GlobalFunctionHeader"">
      \b
      [\d\w_]+  # an identifier
      (?=\s*\=\s*\{) # followed by ""= {""
    </Rule>

    <!-- #define -->
    <Rule color=""Preprocessor"">
      (\#define|\#ifndef)            #starting with ""#define""
      (\s+[a-zA-Z][a-zA-Z0-9_]*)?  #a macro identifyer
    </Rule>

    <!-- #else -->
    <Rule color=""Preprocessor"">
      \#else
    </Rule>

    <!-- #endif -->
    <Rule color=""Preprocessor"">
      \#endif
    </Rule>

    <!-- #include -->
    <Rule color=""Preprocessor"">
      \#include
    </Rule>

    <!-- #region -->
    <Rule color=""Preprocessor"">
      \#region
    </Rule>

    <!-- #endregion -->
    <Rule color=""Preprocessor"">
      \#endregion
    </Rule>

    <Rule color=""Preprocessor"">
      \\$
    </Rule>
      
    <Rule color=""NumberLiterals"">
      0x[0-9A-Fa-f]+
    </Rule>

    <!-- Digits -->
    <Rule color=""NumberLiterals"">
      -+                   #starting with a minus
      [0-9]                #integer
      |
      (	\b\d+(\.[0-9]+)?   #number with optional floating point
      |	\.[0-9]+           #or just starting with floating point
      )
    </Rule>

  </RuleSet>
</SyntaxDefinition>
".Trim();
            }
        }

        private static string XshdContentSqf
        {
            get
            {
                return @"
<?xml version=""1.0""?>
<SyntaxDefinition name=""Sqf"" extensions="".sqf"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <!-- The named colors 'Comment' and 'String' are used in SharpDevelop to detect if a line is inside a multiline string/comment -->
  <Color name=""Comment"" foreground=""#%CommentsForeColor%"" fontWeight=""%CommentsBold%"" fontStyle=""%CommentsItalic%"" exampleText=""// comment"" />
  <Color name=""String"" foreground=""#%StringsForeColor%"" fontWeight=""%StringsBold%"" fontStyle=""%StringsItalic%"" exampleText=""string text = &quot;Hello, World!&quot;""/>
  <Color name=""Brackets"" foreground=""#%BracketsForeColor%"" fontWeight=""%BracketsBold%"" fontStyle=""%BracketsItalic%"" exampleText=""([{}])"" />
  <Color name=""PrivateVariable"" foreground=""#%PrivateVariableForeColor%"" fontWeight=""%PrivateVariableBold%"" fontStyle=""%PrivateVariableItalic%"" exampleText=""_privateVariable""/>
  <Color name=""Keywords"" foreground=""#%KeywordsForeColor%"" fontWeight=""%KeywordsBold%"" fontStyle=""%KeywordsItalic%"" exampleText=""if (a) then {} else {}""/>
  <Color name=""Operators"" foreground=""#%OperatorsForeColor%"" fontWeight=""%OperatorsBold%"" fontStyle=""%OperatorsItalic%"" exampleText=""== !="" />
  <Color name=""GlobalFunctionHeader"" foreground=""#%GlobalFunctionHeaderForeColor%"" fontWeight=""%GlobalFunctionHeaderBold%"" fontStyle=""%GlobalFunctionHeaderItalic%"" exampleText=""o.ToString();""/>
  <Color name=""Preprocessor"" foreground=""#%PreprocessorForeColor%"" fontWeight=""%PreprocessorBold%"" fontStyle=""%PreprocessorItalic%"" exampleText=""#region Title"" />
  <Color name=""NumberLiterals"" foreground=""#%NumberLiteralsForeColor%"" fontWeight=""%NumberLiteralsBold%"" fontStyle=""%NumberLiteralsItalic%"" exampleText=""3.1415f""/>
  <Color name=""ScopeAndBreakCommands"" foreground=""#%ScopeAndBreakCommandsForeColor%"" fontWeight=""%ScopeAndBreakCommandsBold%"" fontStyle=""%ScopeAndBreakCommandsItalic%"" exampleText=""continue; return null;""/>
  <Color name=""TryCatchKeywords"" foreground=""#%KeywordsForeColor%"" fontWeight=""%KeywordsBold%"" fontStyle=""%KeywordsItalic%"" exampleText=""try {} catch {} finally {}""/>
  <Color name=""ValueLiterals"" foreground=""#%ValueLiteralsForeColor%"" fontWeight=""%ValueLiteralsBold%"" fontStyle=""%ValueLiteralsItalic%"" exampleText=""b = false; a = true;"" />

  <Property name=""DocCommentMarker"" value=""///"" />

  <RuleSet name=""CommentMarkerSet"">
    <Keywords fontWeight=""bold"" foreground=""Red"">
      <Word>TODO</Word>
      <Word>FIXME</Word>
    </Keywords>
    <Keywords fontWeight=""bold"" foreground=""#E0E000"">
      <Word>HACK</Word>
      <Word>UNDONE</Word>
    </Keywords>
  </RuleSet>

  <!-- This is the main ruleset. -->
  <RuleSet ignoreCase=""true"">
    
    <!--<Span color=""Preprocessor"">
      <Begin>\#</Begin>
      <RuleSet name=""PreprocessorSet"">
        <Span>
          --><!-- preprocessor directives that allows comments --><!--
          <Begin fontWeight=""bold"">
            (undef|if|elif|else|endif|line)\b
          </Begin>
          <RuleSet>
            <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
              <Begin>//</Begin>
            </Span>
          </RuleSet>
        </Span>
        <Span>
          --><!-- preprocessor directives that don't allow comments --><!--
          <Begin fontWeight=""bold"">
            (region|endregion|error|warning|pragma)\b
          </Begin>
        </Span>
      </RuleSet>
    </Span>-->

    <!--<Span color=""Comment"">
      <Begin color=""XmlDoc/DocComment"">///</Begin>
      <RuleSet>
        <Import ruleSet=""XmlDoc/DocCommentSet""/>
        <Import ruleSet=""CommentMarkerSet""/>
      </RuleSet>
    </Span>-->

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
      <Begin>//</Begin>
    </Span>

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"" multiline=""true"">
      <Begin>/\*</Begin>
      <End>\*/</End>
    </Span>

    <Span color=""String"" multiline=""true"">
      <Begin>""</Begin>
      <End>""</End>
      <RuleSet>
        <!-- span for escape sequences -->
        <Span begin=""\\"" end="".""/>
      </RuleSet>
    </Span>

    <Keywords color=""ValueLiterals"">
      <Word>true</Word>
      <Word>false</Word>
      <Word>east</Word>
      <Word>west</Word>
      <Word>blufor</Word>
      <Word>opfor</Word>
      <Word>civilian</Word>
      <Word>resistance</Word>
      <Word>independent</Word>
      <Word>sideEmpty</Word>
      <Word>sideAmbientLife</Word>
      <Word>sideLogic</Word>
      <Word>objNull</Word>
      <Word>controlNull</Word>
      <Word>displayNull</Word>
      <Word>grpNull</Word>
      <Word>locationNull</Word>
      <Word>taskNull</Word>
      <Word>scriptNull</Word>
      <Word>configNull</Word>
      <Word>classNull</Word>
      <Word>nil</Word>
    </Keywords>

    <Keywords color=""Keywords"">
      <Word>params</Word>
      <Word>if</Word>
      <Word>then</Word>
      <Word>exitWith</Word>
      <Word>else</Word>
      <Word>switch</Word>
      <Word>do</Word>
      <Word>case</Word>
      <Word>default</Word>
      <Word>for</Word>
      <Word>forEach</Word>
      <Word>in</Word>
      <Word>while</Word>
      <Word>private</Word>
    </Keywords>

    <Keywords color=""ScopeAndBreakCommands"">
      <Word>scopeName</Word>
      <Word>breakTo</Word>
      <Word>breakOut</Word>
    </Keywords>

    <Keywords color=""TryCatchKeywords"">
      <Word>try</Word>
      <Word>catch</Word>
    </Keywords>

    <Rule color=""PrivateVariable"">
      (?&lt;![\d\w])_[_a-zA-Z0-9]+ # private variable
    </Rule>
    
    <Rule color=""Brackets"">
      [\{\[\(\)\]\}] # brackets
    </Rule>

    <Rule color=""Operators"">
      (==|!=|&lt;=?|&gt;=?|\!|&amp;&amp;|\|\|) # operators
    </Rule>

    <!-- Mark previous rule-->
    <Rule color=""GlobalFunctionHeader"">
      \b
      [\d\w_]+  # an identifier
      (?=\s*\=\s*\{) # followed by ""= {""
    </Rule>

    <!-- #define -->
    <Rule color=""Preprocessor"">
      (\#define|\#ifndef)            #starting with ""#define""
      (\s+[a-zA-Z][a-zA-Z0-9_]*)?  #a macro identifyer
    </Rule>

    <!-- #else -->
    <Rule color=""Preprocessor"">
      \#else
    </Rule>

    <!-- #endif -->
    <Rule color=""Preprocessor"">
      \#endif
    </Rule>

    <!-- #include -->
    <Rule color=""Preprocessor"">
      \#include
    </Rule>

    <!-- #region -->
    <Rule color=""Preprocessor"">
      \#region
    </Rule>

    <!-- #endregion -->
    <Rule color=""Preprocessor"">
      \#endregion
    </Rule>

    <Rule color=""Preprocessor"">
      \\$
    </Rule>
      
    <Rule color=""NumberLiterals"">
      0x[0-9A-Fa-f]+
    </Rule>

    <!-- Digits -->
    <Rule color=""NumberLiterals"">
      -+                   #starting with a minus
      [0-9]                #integer
      |
      (	\b\d+(\.[0-9]+)?   #number with optional floating point
      |	\.[0-9]+           #or just starting with floating point
      )
    </Rule>

  </RuleSet>
</SyntaxDefinition>
".Trim();
            }
        }

        private static string XshdContentSqm
        {
            get
            {
                return @"
<?xml version=""1.0""?>
<SyntaxDefinition name=""Sqf"" extensions="".sqf"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <!-- The named colors 'Comment' and 'String' are used in SharpDevelop to detect if a line is inside a multiline string/comment -->
  <Color name=""Comment"" foreground=""#%CommentsForeColor%"" fontWeight=""%CommentsBold%"" fontStyle=""%CommentsItalic%"" exampleText=""// comment"" />
  <Color name=""String"" foreground=""#%StringsForeColor%"" fontWeight=""%StringsBold%"" fontStyle=""%StringsItalic%"" exampleText=""string text = &quot;Hello, World!&quot;""/>
  <Color name=""ClassItems"" foreground=""#%ClassKeywordForeColor%"" fontWeight=""%ClassKeywordBold%"" fontStyle=""%ClassKeywordItalic%"" exampleText=""namespace Engima"" />
  <Color name=""Brackets"" foreground=""#%BracketsForeColor%"" fontWeight=""%BracketsBold%"" fontStyle=""%BracketsItalic%"" exampleText=""([{}])"" />
  <Color name=""NativeTypes"" foreground=""#%TypesForeColor%"" fontWeight=""%TypesBold%"" fontStyle=""%TypesItalic%"" exampleText=""namespace Engima"" />
  <Color name=""Keywords"" foreground=""#%KeywordsForeColor%"" fontWeight=""%KeywordsBold%"" fontStyle=""%KeywordsItalic%"" exampleText=""if (a) then {} else {}""/>
  <Color name=""Operators"" foreground=""#%OperatorsForeColor%"" fontWeight=""%OperatorsBold%"" fontStyle=""%OperatorsItalic%"" exampleText=""== !="" />
  <Color name=""GlobalFunctionHeader"" foreground=""#%GlobalFunctionHeaderForeColor%"" fontWeight=""%GlobalFunctionHeaderBold%"" fontStyle=""%GlobalFunctionHeaderItalic%"" exampleText=""o.ToString();""/>
  <Color name=""Preprocessor"" foreground=""#%PreprocessorForeColor%"" fontWeight=""%PreprocessorBold%"" fontStyle=""%PreprocessorItalic%"" exampleText=""#region Title"" />
  <Color name=""NumberLiterals"" foreground=""#%NumberLiteralsForeColor%"" fontWeight=""%NumberLiteralsBold%"" fontStyle=""%NumberLiteralsItalic%"" exampleText=""3.1415f""/>
  <Color name=""ValueLiterals"" foreground=""#%ValueLiteralsForeColor%"" fontWeight=""%ValueLiteralsBold%"" fontStyle=""%ValueLiteralsItalic%"" exampleText=""b = false; a = true;"" />

  <Property name=""DocCommentMarker"" value=""///"" />

  <RuleSet name=""CommentMarkerSet"">
    <Keywords fontWeight=""bold"" foreground=""Red"">
      <Word>TODO</Word>
      <Word>FIXME</Word>
    </Keywords>
    <Keywords fontWeight=""bold"" foreground=""#E0E000"">
      <Word>HACK</Word>
      <Word>UNDONE</Word>
    </Keywords>
  </RuleSet>

  <!-- This is the main ruleset. -->
  <RuleSet ignoreCase=""true"">
    
    <!--<Span color=""Preprocessor"">
      <Begin>\#</Begin>
      <RuleSet name=""PreprocessorSet"">
        <Span>
          --><!-- preprocessor directives that allows comments --><!--
          <Begin fontWeight=""bold"">
            (undef|if|elif|else|endif|line)\b
          </Begin>
          <RuleSet>
            <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
              <Begin>//</Begin>
            </Span>
          </RuleSet>
        </Span>
        <Span>
          --><!-- preprocessor directives that don't allow comments --><!--
          <Begin fontWeight=""bold"">
            (region|endregion|error|warning|pragma)\b
          </Begin>
        </Span>
      </RuleSet>
    </Span>-->

    <!--<Span color=""Comment"">
      <Begin color=""XmlDoc/DocComment"">///</Begin>
      <RuleSet>
        <Import ruleSet=""XmlDoc/DocCommentSet""/>
        <Import ruleSet=""CommentMarkerSet""/>
      </RuleSet>
    </Span>-->

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
      <Begin>//</Begin>
    </Span>

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"" multiline=""true"">
      <Begin>/\*</Begin>
      <End>\*/</End>
    </Span>

    <Rule color=""NativeTypes"">
      (?&lt;=class\s+)
      [A-Za-z0-9_]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=class\s+[A-Za-z0-9_]+\s*:\s*)
      [A-Za-z0-9_]+
    </Rule>

    <Span color=""String"" multiline=""true"">
      <Begin>""</Begin>
      <End>""</End>
      <RuleSet>
        <!-- span for escape sequences -->
        <Span begin=""\\"" end="".""/>
      </RuleSet>
    </Span>

    <Keywords color=""ValueLiterals"">
      <Word>true</Word>
      <Word>false</Word>
    </Keywords>

    <Keywords color=""ClassItems"">
      <Word>class</Word>
    </Keywords>

    <Rule color=""Brackets"">
      [\{\[\(\)\]\}] # brackets
    </Rule>

    <Rule color=""Operators"">
      (==|!=|&lt;=?|&gt;=?|\!|&amp;&amp;|\|\|) # operators
    </Rule>

    <!-- #define -->
    <Rule color=""Preprocessor"">
      (\#define|\#ifndef)            #starting with ""#define""
      (\s+[a-zA-Z][a-zA-Z0-9_]*)?  #a macro identifyer
    </Rule>

    <!-- #else -->
    <Rule color=""Preprocessor"">
      \#else
    </Rule>

    <!-- #endif -->
    <Rule color=""Preprocessor"">
      \#endif
    </Rule>

    <!-- #include -->
    <Rule color=""Preprocessor"">
      \#include
    </Rule>

    <!-- #region -->
    <Rule color=""Preprocessor"">
      \#region
    </Rule>

    <!-- #endregion -->
    <Rule color=""Preprocessor"">
      \#endregion
    </Rule>

    <Rule color=""Preprocessor"">
      \\$
    </Rule>
      
    <Rule color=""NumberLiterals"">
      0x[0-9A-Fa-f]+
    </Rule>

    <!-- Digits -->
    <Rule color=""NumberLiterals"">
      -+                   #starting with a minus
      [0-9]                #integer
      |
      (	\b\d+(\.[0-9]+)?   #number with optional floating point
      |	\.[0-9]+           #or just starting with floating point
      )
    </Rule>

  </RuleSet>
</SyntaxDefinition>
".Trim();
            }
        }

        private static string XshdContentExt
        {
            get
            {
                return @"
<?xml version=""1.0""?>
<SyntaxDefinition name=""Sqf"" extensions="".sqf"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <!-- The named colors 'Comment' and 'String' are used in SharpDevelop to detect if a line is inside a multiline string/comment -->
  <Color name=""Comment"" foreground=""#%CommentsForeColor%"" fontWeight=""%CommentsBold%"" fontStyle=""%CommentsItalic%"" exampleText=""// comment"" />
  <Color name=""String"" foreground=""#%StringsForeColor%"" fontWeight=""%StringsBold%"" fontStyle=""%StringsItalic%"" exampleText=""string text = &quot;Hello, World!&quot;""/>
  <Color name=""ClassItems"" foreground=""#%ClassKeywordForeColor%"" fontWeight=""%ClassKeywordBold%"" fontStyle=""%ClassKeywordItalic%"" exampleText=""namespace Engima"" />
  <Color name=""Brackets"" foreground=""#%BracketsForeColor%"" fontWeight=""%BracketsBold%"" fontStyle=""%BracketsItalic%"" exampleText=""([{}])"" />
  <Color name=""NativeTypes"" foreground=""#%TypesForeColor%"" fontWeight=""%TypesBold%"" fontStyle=""%TypesItalic%"" exampleText=""namespace Engima"" />
  <Color name=""Keywords"" foreground=""#%KeywordsForeColor%"" fontWeight=""%KeywordsBold%"" fontStyle=""%KeywordsItalic%"" exampleText=""if (a) then {} else {}""/>
  <Color name=""Operators"" foreground=""#%OperatorsForeColor%"" fontWeight=""%OperatorsBold%"" fontStyle=""%OperatorsItalic%"" exampleText=""== !="" />
  <Color name=""GlobalFunctionHeader"" foreground=""#%GlobalFunctionHeaderForeColor%"" fontWeight=""%GlobalFunctionHeaderBold%"" fontStyle=""%GlobalFunctionHeaderItalic%"" exampleText=""o.ToString();""/>
  <Color name=""Preprocessor"" foreground=""#%PreprocessorForeColor%"" fontWeight=""%PreprocessorBold%"" fontStyle=""%PreprocessorItalic%"" exampleText=""#region Title"" />
  <Color name=""NumberLiterals"" foreground=""#%NumberLiteralsForeColor%"" fontWeight=""%NumberLiteralsBold%"" fontStyle=""%NumberLiteralsItalic%"" exampleText=""3.1415f""/>
  <Color name=""ValueLiterals"" foreground=""#%ValueLiteralsForeColor%"" fontWeight=""%ValueLiteralsBold%"" fontStyle=""%ValueLiteralsItalic%"" exampleText=""b = false; a = true;"" />

  <Property name=""DocCommentMarker"" value=""///"" />

  <RuleSet name=""CommentMarkerSet"">
    <Keywords fontWeight=""bold"" foreground=""Red"">
      <Word>TODO</Word>
      <Word>FIXME</Word>
    </Keywords>
    <Keywords fontWeight=""bold"" foreground=""#E0E000"">
      <Word>HACK</Word>
      <Word>UNDONE</Word>
    </Keywords>
  </RuleSet>

  <!-- This is the main ruleset. -->
  <RuleSet ignoreCase=""true"">
    
    <!--<Span color=""Preprocessor"">
      <Begin>\#</Begin>
      <RuleSet name=""PreprocessorSet"">
        <Span>
          --><!-- preprocessor directives that allows comments --><!--
          <Begin fontWeight=""bold"">
            (undef|if|elif|else|endif|line)\b
          </Begin>
          <RuleSet>
            <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
              <Begin>//</Begin>
            </Span>
          </RuleSet>
        </Span>
        <Span>
          --><!-- preprocessor directives that don't allow comments --><!--
          <Begin fontWeight=""bold"">
            (region|endregion|error|warning|pragma)\b
          </Begin>
        </Span>
      </RuleSet>
    </Span>-->

    <!--<Span color=""Comment"">
      <Begin color=""XmlDoc/DocComment"">///</Begin>
      <RuleSet>
        <Import ruleSet=""XmlDoc/DocCommentSet""/>
        <Import ruleSet=""CommentMarkerSet""/>
      </RuleSet>
    </Span>-->

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
      <Begin>//</Begin>
    </Span>

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"" multiline=""true"">
      <Begin>/\*</Begin>
      <End>\*/</End>
    </Span>

    <Rule color=""NativeTypes"">
      (?&lt;=class\s+)
      [A-Za-z0-9_]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=class\s+[A-Za-z0-9_]+\s*:\s*)
      [A-Za-z0-9_]+
    </Rule>

    <Span color=""String"">
      <Begin>""</Begin>
      <End>""</End>
      <RuleSet>
        <!-- span for escape sequences -->
        <Span begin=""\\"" end="".""/>
      </RuleSet>
    </Span>

    <Keywords color=""ValueLiterals"">
      <Word>true</Word>
      <Word>false</Word>
    </Keywords>

    <Keywords color=""ClassItems"">
      <Word>class</Word>
    </Keywords>

    <Rule color=""Brackets"">
      [\{\[\(\)\]\}] # brackets
    </Rule>

    <Rule color=""Operators"">
      (==|!=|&lt;=?|&gt;=?|\!|&amp;&amp;|\|\|) # operators
    </Rule>

    <!-- #define -->
    <Rule color=""Preprocessor"">
      (\#define|\#ifndef)            #starting with ""#define""
      (\s+[a-zA-Z][a-zA-Z0-9_]*)?  #a macro identifyer
    </Rule>

    <!-- #else -->
    <Rule color=""Preprocessor"">
      \#else
    </Rule>

    <!-- #endif -->
    <Rule color=""Preprocessor"">
      \#endif
    </Rule>

    <!-- #include -->
    <Rule color=""Preprocessor"">
      \#include
    </Rule>

    <!-- #region -->
    <Rule color=""Preprocessor"">
      \#region
    </Rule>

    <!-- #endregion -->
    <Rule color=""Preprocessor"">
      \#endregion
    </Rule>

    <Rule color=""Preprocessor"">
      \\$
    </Rule>
      
    <Rule color=""NumberLiterals"">
      0x[0-9A-Fa-f]+
    </Rule>

    <!-- Digits -->
    <Rule color=""NumberLiterals"">
      -+                   #starting with a minus
      [0-9]                #integer
      |
      (	\b\d+(\.[0-9]+)?   #number with optional floating point
      |	\.[0-9]+           #or just starting with floating point
      )
    </Rule>

  </RuleSet>
</SyntaxDefinition>
".Trim();
            }
        }

        private static string XshdContentCpp
        {
            get
            {
                return @"
<?xml version=""1.0""?>
<SyntaxDefinition name=""Sqf"" extensions="".sqf"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <!-- The named colors 'Comment' and 'String' are used in SharpDevelop to detect if a line is inside a multiline string/comment -->
  <Color name=""Comment"" foreground=""#%CommentsForeColor%"" fontWeight=""%CommentsBold%"" fontStyle=""%CommentsItalic%"" exampleText=""// comment"" />
  <Color name=""String"" foreground=""#%StringsForeColor%"" fontWeight=""%StringsBold%"" fontStyle=""%StringsItalic%"" exampleText=""string text = &quot;Hello, World!&quot;""/>
  <Color name=""ClassItems"" foreground=""#%ClassKeywordForeColor%"" fontWeight=""%ClassKeywordBold%"" fontStyle=""%ClassKeywordItalic%"" exampleText=""namespace Engima"" />
  <Color name=""Brackets"" foreground=""#%BracketsForeColor%"" fontWeight=""%BracketsBold%"" fontStyle=""%BracketsItalic%"" exampleText=""([{}])"" />
  <Color name=""NativeTypes"" foreground=""#%TypesForeColor%"" fontWeight=""%TypesBold%"" fontStyle=""%TypesItalic%"" exampleText=""namespace Engima"" />
  <Color name=""Keywords"" foreground=""#%KeywordsForeColor%"" fontWeight=""%KeywordsBold%"" fontStyle=""%KeywordsItalic%"" exampleText=""if (a) then {} else {}""/>
  <Color name=""Operators"" foreground=""#%OperatorsForeColor%"" fontWeight=""%OperatorsBold%"" fontStyle=""%OperatorsItalic%"" exampleText=""== !="" />
  <Color name=""GlobalFunctionHeader"" foreground=""#%GlobalFunctionHeaderForeColor%"" fontWeight=""%GlobalFunctionHeaderBold%"" fontStyle=""%GlobalFunctionHeaderItalic%"" exampleText=""o.ToString();""/>
  <Color name=""Preprocessor"" foreground=""#%PreprocessorForeColor%"" fontWeight=""%PreprocessorBold%"" fontStyle=""%PreprocessorItalic%"" exampleText=""#region Title"" />
  <Color name=""NumberLiterals"" foreground=""#%NumberLiteralsForeColor%"" fontWeight=""%NumberLiteralsBold%"" fontStyle=""%NumberLiteralsItalic%"" exampleText=""3.1415f""/>

  <Property name=""DocCommentMarker"" value=""///"" />

  <RuleSet name=""CommentMarkerSet"">
    <Keywords fontWeight=""bold"" foreground=""Red"">
      <Word>TODO</Word>
      <Word>FIXME</Word>
    </Keywords>
    <Keywords fontWeight=""bold"" foreground=""#E0E000"">
      <Word>HACK</Word>
      <Word>UNDONE</Word>
    </Keywords>
  </RuleSet>

  <!-- This is the main ruleset. -->
  <RuleSet ignoreCase=""true"">
    
    <!--<Span color=""Preprocessor"">
      <Begin>\#</Begin>
      <RuleSet name=""PreprocessorSet"">
        <Span>
          --><!-- preprocessor directives that allows comments --><!--
          <Begin fontWeight=""bold"">
            (undef|if|elif|else|endif|line)\b
          </Begin>
          <RuleSet>
            <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
              <Begin>//</Begin>
            </Span>
          </RuleSet>
        </Span>
        <Span>
          --><!-- preprocessor directives that don't allow comments --><!--
          <Begin fontWeight=""bold"">
            (region|endregion|error|warning|pragma)\b
          </Begin>
        </Span>
      </RuleSet>
    </Span>-->

    <!--<Span color=""Comment"">
      <Begin color=""XmlDoc/DocComment"">///</Begin>
      <RuleSet>
        <Import ruleSet=""XmlDoc/DocCommentSet""/>
        <Import ruleSet=""CommentMarkerSet""/>
      </RuleSet>
    </Span>-->

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
      <Begin>//</Begin>
    </Span>

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"" multiline=""true"">
      <Begin>/\*</Begin>
      <End>\*/</End>
    </Span>

    <Rule color=""NativeTypes"">
      (?&lt;=class\s+)
      [A-Za-z0-9_]+
    </Rule>

    <Rule color=""NativeTypes"">
      (?&lt;=class\s+[A-Za-z0-9_]+\s*:\s*)
      [A-Za-z0-9_]+
    </Rule>

    <Span color=""String"">
      <Begin>""</Begin>
      <End>""</End>
      <RuleSet>
        <!-- span for escape sequences -->
        <Span begin=""\\"" end="".""/>
      </RuleSet>
    </Span>

    <Keywords color=""ClassItems"">
      <Word>class</Word>
    </Keywords>

    <Rule color=""Brackets"">
      [\{\[\(\)\]\}] # brackets
    </Rule>

    <Rule color=""Operators"">
      (==|!=|&lt;=?|&gt;=?|\!|&amp;&amp;|\|\|) # operators
    </Rule>

    <!-- #define -->
    <Rule color=""Preprocessor"">
      (\#define|\#ifndef)            #starting with ""#define""
      (\s+[a-zA-Z][a-zA-Z0-9_]*)?  #a macro identifyer
    </Rule>

    <!-- #else -->
    <Rule color=""Preprocessor"">
      \#else
    </Rule>

    <!-- #endif -->
    <Rule color=""Preprocessor"">
      \#endif
    </Rule>

    <!-- #include -->
    <Rule color=""Preprocessor"">
      \#include
    </Rule>

    <!-- #region -->
    <Rule color=""Preprocessor"">
      \#region
    </Rule>

    <!-- #endregion -->
    <Rule color=""Preprocessor"">
      \#endregion
    </Rule>

    <Rule color=""Preprocessor"">
      \\$
    </Rule>
      
    <Rule color=""NumberLiterals"">
      0x[0-9A-Fa-f]+
    </Rule>

    <!-- Digits -->
    <Rule color=""NumberLiterals"">
      -+                   #starting with a minus
      [0-9]                #integer
      |
      (	\b\d+(\.[0-9]+)?   #number with optional floating point
      |	\.[0-9]+           #or just starting with floating point
      )
    </Rule>

  </RuleSet>
</SyntaxDefinition>
".Trim();
            }
        }

        private static string XshdContentBasic
        {
            get
            {
                return @"
<?xml version=""1.0""?>
<SyntaxDefinition name=""Sqf"" extensions="".sqf"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <!-- The named colors 'Comment' and 'String' are used in SharpDevelop to detect if a line is inside a multiline string/comment -->
  <Color name=""Comment"" foreground=""#%CommentsForeColor%"" fontWeight=""%CommentsBold%"" fontStyle=""%CommentsItalic%"" exampleText=""// comment"" />
  <Color name=""String"" foreground=""#%StringsForeColor%"" fontWeight=""%StringsBold%"" fontStyle=""%StringsItalic%"" exampleText=""string text = &quot;Hello, World!&quot;""/>
  <Color name=""Brackets"" foreground=""#%BracketsForeColor%"" fontWeight=""%BracketsBold%"" fontStyle=""%BracketsItalic%"" exampleText=""([{}])"" />
  <Color name=""Keywords"" foreground=""#%KeywordsForeColor%"" fontWeight=""%KeywordsBold%"" fontStyle=""%KeywordsItalic%"" exampleText=""if (a) then {} else {}""/>
  <Color name=""Operators"" foreground=""#%OperatorsForeColor%"" fontWeight=""%OperatorsBold%"" fontStyle=""%OperatorsItalic%"" exampleText=""== !="" />
  <Color name=""GlobalFunctionHeader"" foreground=""#%GlobalFunctionHeaderForeColor%"" fontWeight=""%GlobalFunctionHeaderBold%"" fontStyle=""%GlobalFunctionHeaderItalic%"" exampleText=""o.ToString();""/>
  <Color name=""Preprocessor"" foreground=""#%PreprocessorForeColor%"" fontWeight=""%PreprocessorBold%"" fontStyle=""%PreprocessorItalic%"" exampleText=""#region Title"" />
  <Color name=""NumberLiterals"" foreground=""#%NumberLiteralsForeColor%"" fontWeight=""%NumberLiteralsBold%"" fontStyle=""%NumberLiteralsItalic%"" exampleText=""3.1415f""/>
  <Color name=""ValueLiterals"" foreground=""#%ValueLiteralsForeColor%"" fontWeight=""%ValueLiteralsBold%"" fontStyle=""%ValueLiteralsItalic%"" exampleText=""b = false; a = true;"" />

  <Property name=""DocCommentMarker"" value=""///"" />

  <RuleSet name=""CommentMarkerSet"">
    <Keywords fontWeight=""bold"" foreground=""Red"">
      <Word>TODO</Word>
      <Word>FIXME</Word>
    </Keywords>
    <Keywords fontWeight=""bold"" foreground=""#E0E000"">
      <Word>HACK</Word>
      <Word>UNDONE</Word>
    </Keywords>
  </RuleSet>

  <!-- This is the main ruleset. -->
  <RuleSet ignoreCase=""true"">
    
    <!--<Span color=""Preprocessor"">
      <Begin>\#</Begin>
      <RuleSet name=""PreprocessorSet"">
        <Span>
          --><!-- preprocessor directives that allows comments --><!--
          <Begin fontWeight=""bold"">
            (undef|if|elif|else|endif|line)\b
          </Begin>
          <RuleSet>
            <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
              <Begin>//</Begin>
            </Span>
          </RuleSet>
        </Span>
        <Span>
          --><!-- preprocessor directives that don't allow comments --><!--
          <Begin fontWeight=""bold"">
            (region|endregion|error|warning|pragma)\b
          </Begin>
        </Span>
      </RuleSet>
    </Span>-->

    <!--<Span color=""Comment"">
      <Begin color=""XmlDoc/DocComment"">///</Begin>
      <RuleSet>
        <Import ruleSet=""XmlDoc/DocCommentSet""/>
        <Import ruleSet=""CommentMarkerSet""/>
      </RuleSet>
    </Span>-->

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"">
      <Begin>//</Begin>
    </Span>

    <Span color=""Comment"" ruleSet=""CommentMarkerSet"" multiline=""true"">
      <Begin>/\*</Begin>
      <End>\*/</End>
    </Span>

    <Span color=""String"">
      <Begin>""</Begin>
      <End>""</End>
      <RuleSet>
        <!-- span for escape sequences -->
        <Span begin=""\\"" end="".""/>
      </RuleSet>
    </Span>

    <Keywords color=""ValueLiterals"">
      <Word>true</Word>
      <Word>false</Word>
    </Keywords>

    <Rule color=""Brackets"">
      [\{\[\(\)\]\}] # brackets
    </Rule>

    <Rule color=""Operators"">
      (==|!=|&lt;=?|&gt;=?|\!|&amp;&amp;|\|\|) # operators
    </Rule>

    <!-- #define -->
    <Rule color=""Preprocessor"">
      (\#define|\#ifndef)            #starting with ""#define""
      (\s+[a-zA-Z][a-zA-Z0-9_]*)?  #a macro identifyer
    </Rule>

    <!-- #else -->
    <Rule color=""Preprocessor"">
      \#else
    </Rule>

    <!-- #endif -->
    <Rule color=""Preprocessor"">
      \#endif
    </Rule>

    <!-- #include -->
    <Rule color=""Preprocessor"">
      \#include
    </Rule>

    <!-- #region -->
    <Rule color=""Preprocessor"">
      \#region
    </Rule>

    <!-- #endregion -->
    <Rule color=""Preprocessor"">
      \#endregion
    </Rule>

    <Rule color=""Preprocessor"">
      \\$
    </Rule>
      
    <Rule color=""NumberLiterals"">
      0x[0-9A-Fa-f]+
    </Rule>

    <!-- Digits -->
    <Rule color=""NumberLiterals"">
      -+                   #starting with a minus
      [0-9]                #integer
      |
      (	\b\d+(\.[0-9]+)?   #number with optional floating point
      |	\.[0-9]+           #or just starting with floating point
      )
    </Rule>

  </RuleSet>
</SyntaxDefinition>
".Trim();
            }
        }

        private static string GetXshdContent(string extension)
        {
            if (extension.ToLower() == ".sqx")
            {
                return XshdContentSqx;
            }
            else if (extension.ToLower() == ".sqf")
            {
                return XshdContentSqf;
            }
            else if (extension.ToLower() == ".ext")
            {
                return XshdContentExt;
            }
            else if (extension.ToLower() == ".sqm")
            {
                return XshdContentSqm;
            }

            return XshdContentBasic;
        }

        public static string GetDefinition(string extension, Theme theme = null)
        {
            if (theme == null && LoadedTheme != null)
            {
                theme = LoadedTheme;
            }

            ColorDefinition stringLiterals = theme.GetColorDefinition("StringLiterals");
            ColorDefinition comments = theme.GetColorDefinition("Comments");
            ColorDefinition classKeywords = theme.GetColorDefinition("ClassKeywords");
            ColorDefinition brackets = theme.GetColorDefinition("Brackets");
            ColorDefinition types = theme.GetColorDefinition("Types");
            ColorDefinition privateVariables = theme.GetColorDefinition("PrivateVariables");
            ColorDefinition classMemberNames = theme.GetColorDefinition("ClassMemberNames");
            ColorDefinition keywords = theme.GetColorDefinition("Keywords");
            ColorDefinition operators = theme.GetColorDefinition("Operators");
            ColorDefinition globalFunctions = theme.GetColorDefinition("GlobalFunctions");
            ColorDefinition preprocessorKeywords = theme.GetColorDefinition("PreprocessorKeywords");
            ColorDefinition numberLiterals = theme.GetColorDefinition("NumberLiterals");
            ColorDefinition scopeAndBreakCommands = theme.GetColorDefinition("ScopeAndBreakCommands");
            ColorDefinition constantLiterals = theme.GetColorDefinition("ConstantLiterals");
            ColorDefinition propertyGetSet = theme.GetColorDefinition("PropertyGetSet");

            string xshdContent = GetXshdContent(extension);

            return xshdContent
                .Replace("%CommentsForeColor%", comments.ForeColor)
                .Replace("%CommentsBold%", comments.Bold ? "bold" : "normal")
                .Replace("%CommentsItalic%", comments.Italic ? "italic" : "normal")
                .Replace("%StringsForeColor%", stringLiterals.ForeColor)
                .Replace("%StringsBold%", stringLiterals.Bold ? "bold" : "normal")
                .Replace("%StringsItalic%", stringLiterals.Italic ? "italic" : "normal")
                .Replace("%ClassKeywordForeColor%", classKeywords.ForeColor)
                .Replace("%ClassKeywordBold%", classKeywords.Bold ? "bold" : "normal")
                .Replace("%ClassKeywordItalic%", classKeywords.Italic ? "italic" : "normal")
                .Replace("%BracketsForeColor%", brackets.ForeColor)
                .Replace("%BracketsBold%", brackets.Bold ? "bold" : "normal")
                .Replace("%BracketsItalic%", brackets.Italic ? "italic" : "normal")
                .Replace("%TypesForeColor%", types.ForeColor)
                .Replace("%TypesBold%", types.Bold ? "bold" : "normal")
                .Replace("%TypesItalic%", types.Italic ? "italic" : "normal")
                .Replace("%PrivateVariableForeColor%", privateVariables.ForeColor)
                .Replace("%PrivateVariableBold%", privateVariables.Bold ? "bold" : "normal")
                .Replace("%PrivateVariableItalic%", privateVariables.Italic ? "italic" : "normal")
                .Replace("%ClassMemberNamesForeColor%", classMemberNames.ForeColor)
                .Replace("%ClassMemberNamesBold%", classMemberNames.Bold ? "bold" : "normal")
                .Replace("%ClassMemberNamesItalic%", classMemberNames.Italic ? "italic" : "normal")
                .Replace("%KeywordsForeColor%", keywords.ForeColor)
                .Replace("%KeywordsBold%", keywords.Bold ? "bold" : "normal")
                .Replace("%KeywordsItalic%", keywords.Italic ? "italic" : "normal")
                .Replace("%OperatorsForeColor%", operators.ForeColor)
                .Replace("%OperatorsBold%", operators.Bold ? "bold" : "normal")
                .Replace("%OperatorsItalic%", operators.Italic ? "italic" : "normal")
                .Replace("%GlobalFunctionHeaderForeColor%", globalFunctions.ForeColor)
                .Replace("%GlobalFunctionHeaderBold%", globalFunctions.Bold ? "bold" : "normal")
                .Replace("%GlobalFunctionHeaderItalic%", globalFunctions.Italic ? "italic" : "normal")
                .Replace("%PreprocessorForeColor%", preprocessorKeywords.ForeColor)
                .Replace("%PreprocessorBold%", preprocessorKeywords.Bold ? "bold" : "normal")
                .Replace("%PreprocessorItalic%", preprocessorKeywords.Italic ? "italic" : "normal")
                .Replace("%NumberLiteralsForeColor%", numberLiterals.ForeColor)
                .Replace("%NumberLiteralsBold%", numberLiterals.Bold ? "bold" : "normal")
                .Replace("%NumberLiteralsItalic%", numberLiterals.Italic ? "italic" : "normal")
                .Replace("%ScopeAndBreakCommandsForeColor%", scopeAndBreakCommands.ForeColor)
                .Replace("%ScopeAndBreakCommandsBold%", scopeAndBreakCommands.Bold ? "bold" : "normal")
                .Replace("%ScopeAndBreakCommandsItalic%", scopeAndBreakCommands.Italic ? "italic" : "normal")
                .Replace("%ValueLiteralsForeColor%", constantLiterals.ForeColor)
                .Replace("%ValueLiteralsBold%", constantLiterals.Bold ? "bold" : "normal")
                .Replace("%ValueLiteralsItalic%", constantLiterals.Italic ? "italic" : "normal")
                .Replace("%PropertyGetSetForeColor%", propertyGetSet.ForeColor)
                .Replace("%PropertyGetSetBold%", propertyGetSet.Bold ? "bold" : "normal")
                .Replace("%PropertyGetSetItalic%", propertyGetSet.Italic ? "italic" : "normal");
        }

        public static void WriteThemeToDisc(string filePathName, Theme theme)
        {
            using (var writer = new StreamWriter(filePathName))
            {
                var serializer = new XmlSerializer(typeof(Theme));
                serializer.Serialize(writer, theme);
                writer.Flush();
            }
        }

        public static Theme LoadedTheme { get; private set; }

        public static string LoadTheme(string name)
        {
            try
            {
                XmlSerializer reader = new XmlSerializer(typeof(Theme));

                string fileName = Path.Combine(CurrentApplication.AppDataFolder, "Themes", name + ".xml");
                using (StreamReader file = new StreamReader(fileName))
                {
                    LoadedTheme = (Theme)reader.Deserialize(file);
                    return name;
                }
            }
            catch
            {
                LoadedTheme = Theme.DefaultLight;
                return "Default Light";
            }
        }
    }
}
