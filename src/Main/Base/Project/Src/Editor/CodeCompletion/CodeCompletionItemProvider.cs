// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Refactoring;

namespace ICSharpCode.SharpDevelop.Editor.CodeCompletion
{
	/// <summary>
	/// Base class for completion item providers.
	/// </summary>
	/// <remarks>A completion item provider is not necessary to use code completion - it's
	/// just a helper class.</remarks>
	public abstract class AbstractCompletionItemProvider
	{
		/// <summary>
		/// Shows code completion for the specified editor.
		/// </summary>
		public virtual void ShowCompletion(ITextEditor editor)
		{
			if (editor == null)
				throw new ArgumentNullException("editor");
			ICompletionItemList itemList = GenerateCompletionList(editor);
			if (itemList != null)
				editor.ShowCompletionWindow(itemList);
		}
		
		/// <summary>
		/// Generates the completion list.
		/// </summary>
		public abstract ICompletionItemList GenerateCompletionList(ITextEditor editor);
	}
	
	/// <summary>
	/// Allows creating a <see cref="ICompletionDataList"/> from code-completion information.
	/// </summary>
	public class CodeCompletionItemProvider : AbstractCompletionItemProvider
	{
		/// <inheritdoc/>
		public override ICompletionItemList GenerateCompletionList(ITextEditor editor)
		{
			if (editor == null)
				throw new ArgumentNullException("textEditor");
			ExpressionResult expression = GetExpression(editor);
			return GenerateCompletionListForExpression(editor, expression);
		}
		
		public virtual ExpressionResult GetExpression(ITextEditor editor)
		{
			return GetExpressionFromOffset(editor, editor.Caret.Offset);
		}
		
		protected ExpressionResult GetExpressionFromOffset(ITextEditor editor, int offset)
		{
			if (editor == null)
				throw new ArgumentNullException("editor");
			IDocument document = editor.Document;
			IExpressionFinder expressionFinder = ParserService.GetExpressionFinder(editor.FileName);
			if (expressionFinder == null) {
				return ExpressionResult.Empty;
			} else {
				return expressionFinder.FindExpression(document.Text, offset);
			}
		}
		
		public virtual ICompletionItemList GenerateCompletionListForExpression(ITextEditor editor, ExpressionResult expressionResult)
		{
			if (expressionResult.Expression == null) {
				return null;
			}
			if (LoggingService.IsDebugEnabled) {
				if (expressionResult.Context == ExpressionContext.Default)
					LoggingService.DebugFormatted("GenerateCompletionData for >>{0}<<", expressionResult.Expression);
				else
					LoggingService.DebugFormatted("GenerateCompletionData for >>{0}<<, context={1}", expressionResult.Expression, expressionResult.Context);
			}
			ResolveResult rr = Resolve(editor, expressionResult);
			return GenerateCompletionListForResolveResult(rr, expressionResult.Context);
		}
		
		public virtual ResolveResult Resolve(ITextEditor editor, ExpressionResult expressionResult)
		{
			if (editor == null)
				throw new ArgumentNullException("editor");
			return ParserService.Resolve(expressionResult, editor.Caret.Line, editor.Caret.Column, editor.FileName, editor.Document.Text);
		}
		
		public virtual ICompletionItemList GenerateCompletionListForResolveResult(ResolveResult rr, ExpressionContext context)
		{
			if (rr == null)
				return null;
			IProjectContent callingContent = rr.CallingClass != null ? rr.CallingClass.ProjectContent : null;
			List<ICompletionEntry> arr = rr.GetCompletionData(callingContent ?? ParserService.CurrentProjectContent);
			return GenerateCompletionListForCompletionData(arr, context);
		}
		
		protected virtual DefaultCompletionItemList CreateCompletionItemList()
		{
			return new DefaultCompletionItemList();
		}
		
		protected virtual void InitializeCompletionItemList(DefaultCompletionItemList list)
		{
			list.SortItems();
		}
		
		public virtual ICompletionItemList GenerateCompletionListForCompletionData(List<ICompletionEntry> arr, ExpressionContext context)
		{
			if (arr == null)
				return null;
			
			DefaultCompletionItemList result = CreateCompletionItemList();
			Dictionary<string, CodeCompletionItem> methodItems = new Dictionary<string, CodeCompletionItem>();
			foreach (ICompletionEntry o in arr) {
				if (context != null && !context.ShowEntry(o))
					continue;
				
				IMethod method = o as IMethod;
				if (method != null) {
					CodeCompletionItem codeItem;
					if (methodItems.TryGetValue(method.Name, out codeItem)) {
						codeItem.Overloads++;
						continue;
					}
				}
				
				ICompletionItem item = CreateCompletionItem(o, context);
				if (item != null) {
					result.Items.Add(item);
					CodeCompletionItem codeItem = item as CodeCompletionItem;
					if (method != null && codeItem != null) {
						methodItems[method.Name] = codeItem;
					}
					if (o.Equals(context.SuggestedItem))
						result.SuggestedItem = item;
				}
			}
			InitializeCompletionItemList(result);
			
			if (context.SuggestedItem != null) {
				if (result.SuggestedItem == null) {
					result.SuggestedItem = CreateCompletionItem(context.SuggestedItem, context);
					if (result.SuggestedItem != null) {
						result.Items.Insert(0, result.SuggestedItem);
					}
				}
			}
			return result;
		}
		
		public virtual ICompletionItem CreateCompletionItem(object o, ExpressionContext context)
		{
			IEntity entity = o as IEntity;
			if (entity != null) {
				return new CodeCompletionItem(entity);
			} else if (o is Dom.NRefactoryResolver.KeywordEntry) {
				return new KeywordCompletionItem(o.ToString());
			} else {
				DefaultCompletionItem item = new DefaultCompletionItem(o.ToString());
				if (o is NamespaceEntry)
					item.Image = ClassBrowserIconService.Namespace;
				return item;
			}
		}
	}
	
	public class DotCodeCompletionItemProvider : CodeCompletionItemProvider
	{
		
	}
	
	sealed class KeywordCompletionItem : DefaultCompletionItem
	{
		readonly double priority;
		
		public KeywordCompletionItem(string text) : base(text)
		{
			this.Image = ClassBrowserIconService.Keyword;
			priority = CodeCompletionDataUsageCache.GetPriority("keyword." + this.Text, true);
		}
		
		public override double Priority {
			get { return priority; }
		}
		
		public override void Complete(CompletionContext context)
		{
			CodeCompletionDataUsageCache.IncrementUsage("keyword." + this.Text);
			base.Complete(context);
		}
	}
	
	public class CodeCompletionItem : ICompletionItem
	{
		public double Priority { get; set; }
		
		readonly IEntity entity;
		
		public CodeCompletionItem(IEntity entity)
		{
			if (entity == null)
				throw new ArgumentNullException("entity");
			this.entity = entity;
			
			IAmbience ambience = AmbienceService.GetCurrentAmbience();
			ambience.ConversionFlags = entity is IClass ? ConversionFlags.ShowTypeParameterList : ConversionFlags.None;
			this.Text = ambience.Convert(entity);
			ambience.ConversionFlags = ConversionFlags.StandardConversionFlags;
			description = ambience.Convert(entity);
			this.Image = ClassBrowserIconService.GetIcon(entity);
			this.Overloads = 1;
			
			this.Priority = CodeCompletionDataUsageCache.GetPriority(entity.DotNetName, true);
		}
		
		public IEntity Entity {
			get { return entity; }
		}
		
		public string Text { get; set; }
		
		public int Overloads { get; set; }
		
		public IImage Image { get; set; }
		
		protected void MarkAsUsed()
		{
			CodeCompletionDataUsageCache.IncrementUsage(entity.DotNetName);
		}
		
		public virtual void Complete(CompletionContext context)
		{
			MarkAsUsed();
			
			string insertedText = this.Text;
			bool addUsing = false;
			
			var selectedClass = this.Entity as IClass;
			if (selectedClass != null) {
				// Class is being inserted
				var editor = context.Editor;
				var document = context.Editor.Document;
				
				var position = document.OffsetToPosition(context.StartOffset);
				var nameResult = ParserService.Resolve(new ExpressionResult(selectedClass.Name), position.Line, position.Column, editor.FileName, document.Text);
				var fullNameResult = ParserService.Resolve(new ExpressionResult(selectedClass.FullyQualifiedName), position.Line, position.Column, editor.FileName, document.Text);

				var cu = nameResult.CallingClass.CompilationUnit;
				if (IsKnown(nameResult)) {
					if (IsEqualClass(nameResult, selectedClass)) {
						// Selected name is known in the current context - do nothing
					} else {
						// Selected name is known in the current context but resolves to something else than the user wants to insert
						// (i.e. some other class with the same name closer to current context according to language rules)
						// - the only solution is to insert user's choice fully qualified
						insertedText = selectedClass.FullyQualifiedName;
					}
				} else {
					// The name is unknown - we add a using
					addUsing = true;
				}
				
				context.Editor.Document.Replace(context.StartOffset, context.Length, insertedText);
				context.EndOffset = context.StartOffset + insertedText.Length;
				
				if (addUsing) {
					NamespaceRefactoringService.AddUsingDeclaration(cu, document, selectedClass.Namespace, true);
					ParserService.BeginParse(context.Editor.FileName, context.Editor.Document);
				}
			} else {
				// Something else than a class is being inserted - just insert
				context.Editor.Document.Replace(context.StartOffset, context.Length, insertedText);
				context.EndOffset = context.StartOffset + insertedText.Length;
			}
		}
		
		/// <summary>
		/// Returns false if <paramref name="result" /> is <see cref="UnknownIdentifierResolveResult" /> or something similar.
		/// </summary>
		bool IsKnown(ResolveResult result)
		{
			return !(result is UnknownIdentifierResolveResult || result is UnknownConstructorCallResolveResult);
		}
		
		/// <summary>
		/// Returns true if both parameters refer to the same class.
		/// </summary>
		bool IsEqualClass(ResolveResult nameResult, IClass selectedClass)
		{
			var classResult = nameResult as TypeResolveResult;
			if (classResult == null)
				return false;
			return classResult.ResolvedClass.FullyQualifiedName == selectedClass.FullyQualifiedName;
		}
		
		#region Description
		string description;
		bool descriptionCreated;
		
		public string Description {
			get {
				lock (this) {
					if (!descriptionCreated) {
						descriptionCreated = true;
						if (Overloads > 1) {
							description += Environment.NewLine +
								StringParser.Parse("${res:ICSharpCode.SharpDevelop.DefaultEditor.Gui.Editor.CodeCompletionData.OverloadsCounter}", new string[,] {{"NumOverloads", this.Overloads.ToString()}});
						}
						string entityDoc = entity.Documentation;
						if (!string.IsNullOrEmpty(entityDoc)) {
							string documentation = ConvertDocumentation(entityDoc);
							if (!string.IsNullOrEmpty(documentation)) {
								description += Environment.NewLine + documentation;
							}
						}
					}
					return description;
				}
			}
		}
		
		static readonly Regex whitespace = new Regex(@"\s+");
		
		/// <summary>
		/// Converts the xml documentation string into a plain text string.
		/// </summary>
		public static string ConvertDocumentation(string xmlDocumentation)
		{
			if (string.IsNullOrEmpty(xmlDocumentation))
				return string.Empty;
			
			System.IO.StringReader reader = new System.IO.StringReader("<docroot>" + xmlDocumentation + "</docroot>");
			XmlTextReader xml   = new XmlTextReader(reader);
			StringBuilder ret   = new StringBuilder();
			////Regex whitespace    = new Regex(@"\s+");
			
			try {
				xml.Read();
				do {
					if (xml.NodeType == XmlNodeType.Element) {
						string elname = xml.Name.ToLowerInvariant();
						switch (elname) {
							case "filterpriority":
								xml.Skip();
								break;
							case "remarks":
								ret.Append(Environment.NewLine);
								ret.Append("Remarks:");
								ret.Append(Environment.NewLine);
								break;
							case "example":
								ret.Append(Environment.NewLine);
								ret.Append("Example:");
								ret.Append(Environment.NewLine);
								break;
							case "exception":
								ret.Append(Environment.NewLine);
								ret.Append(GetCref(xml["cref"]));
								ret.Append(": ");
								break;
							case "returns":
								ret.Append(Environment.NewLine);
								ret.Append("Returns: ");
								break;
							case "see":
								ret.Append(GetCref(xml["cref"]));
								ret.Append(xml["langword"]);
								break;
							case "seealso":
								ret.Append(Environment.NewLine);
								ret.Append("See also: ");
								ret.Append(GetCref(xml["cref"]));
								break;
							case "paramref":
								ret.Append(xml["name"]);
								break;
							case "param":
								ret.Append(Environment.NewLine);
								ret.Append(whitespace.Replace(xml["name"].Trim()," "));
								ret.Append(": ");
								break;
							case "value":
								ret.Append(Environment.NewLine);
								ret.Append("Value: ");
								ret.Append(Environment.NewLine);
								break;
							case "br":
							case "para":
								ret.Append(Environment.NewLine);
								break;
						}
					} else if (xml.NodeType == XmlNodeType.Text) {
						ret.Append(whitespace.Replace(xml.Value, " "));
					}
				} while(xml.Read());
			} catch (Exception ex) {
				LoggingService.Debug("Invalid XML documentation: " + ex.Message);
				return xmlDocumentation;
			}
			return ret.ToString();
		}
		
		static string GetCref(string cref)
		{
			if (cref == null || cref.Trim().Length==0) {
				return "";
			}
			if (cref.Length < 2) {
				return cref;
			}
			if (cref.Substring(1, 1) == ":") {
				return cref.Substring(2, cref.Length - 2);
			}
			return cref;
		}
		#endregion
	}
}