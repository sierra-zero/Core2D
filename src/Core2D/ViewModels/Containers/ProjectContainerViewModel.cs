﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Core2D.Model;
using Core2D.Model.History;
using Core2D.ViewModels.Data;
using Core2D.ViewModels.Scripting;
using Core2D.ViewModels.Shapes;
using Core2D.ViewModels.Style;

namespace Core2D.ViewModels.Containers
{
    public partial class ProjectContainerViewModel : ViewModelBase, ISelection
    {
        [AutoNotify] private OptionsViewModel _options;
        [AutoNotify] private IHistory _history;
        [AutoNotify] private ImmutableArray<LibraryViewModel<ShapeStyleViewModel>> _styleLibraries;
        [AutoNotify] private ImmutableArray<LibraryViewModel<GroupShapeViewModel>> _groupLibraries;
        [AutoNotify] private ImmutableArray<DatabaseViewModel> _databases;
        [AutoNotify] private ImmutableArray<TemplateContainerViewModel> _templates;
        [AutoNotify] private ImmutableArray<ScriptViewModel> _scripts;
        [AutoNotify] private ImmutableArray<DocumentContainerViewModel> _documents;
        [AutoNotify] private LibraryViewModel<ShapeStyleViewModel> _currentStyleLibrary;
        [AutoNotify] private LibraryViewModel<GroupShapeViewModel> _currentGroupLibrary;
        [AutoNotify] private DatabaseViewModel _currentDatabase;
        [AutoNotify] private TemplateContainerViewModel _currentTemplate;
        [AutoNotify] private ScriptViewModel _currentScript;
        [AutoNotify] private DocumentContainerViewModel _currentDocument;
        [AutoNotify] private BaseContainerViewModel _currentContainer;
        [AutoNotify] private ViewModelBase _selected;
        [AutoNotify] private ISet<BaseShapeViewModel> _selectedShapes;

        public static IEnumerable<BaseShapeViewModel> GetAllShapes(IEnumerable<BaseShapeViewModel> shapes)
        {
            if (shapes == null)
            {
                yield break;
            }

            foreach (var shape in shapes)
            {
                if (shape is GroupShapeViewModel groupShape)
                {
                    foreach (var s in GetAllShapes(groupShape.Shapes))
                    {
                        yield return s;
                    }

                    yield return shape;
                }
                else
                {
                    yield return shape;
                }
            }
        }

        public static IEnumerable<T> GetAllShapes<T>(IEnumerable<BaseShapeViewModel> shapes)
        {
            return GetAllShapes(shapes)?.Where(s => s is T).Cast<T>();
        }

        public static IEnumerable<T> GetAllShapes<T>(ProjectContainerViewModel project)
        {
            var shapes = project?.Documents
                .SelectMany(d => d.Pages)
                .SelectMany(c => c.Layers)
                .SelectMany(l => l.Shapes);

            return GetAllShapes(shapes)?.Where(s => s is T).Cast<T>();
        }

        public ProjectContainerViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public void SetCurrentDocument(DocumentContainerViewModel document)
        {
            CurrentDocument = document;
            Selected = document;
            SetSelected(document);
        }

        public void SetCurrentContainer(BaseContainerViewModel container)
        {
            CurrentContainer = container;
            Selected = container;
            SetSelected(container);
        }

        public void SetCurrentTemplate(TemplateContainerViewModel template) => CurrentTemplate = template;

        public void SetCurrentScript(ScriptViewModel script) => CurrentScript = script;

        public void SetCurrentDatabase(DatabaseViewModel db) => CurrentDatabase = db;

        public void SetCurrentGroupLibrary(LibraryViewModel<GroupShapeViewModel> libraryViewModel) => CurrentGroupLibrary = libraryViewModel;

        public void SetCurrentStyleLibrary(LibraryViewModel<ShapeStyleViewModel> libraryViewModel) => CurrentStyleLibrary = libraryViewModel;

        public void SelectedChanged() => SetSelected(Selected);

        public void SetSelected(ViewModelBase value)
        {
            if (value is LayerContainerViewModel layer)
            {
                if (layer.Owner is BaseContainerViewModel owner)
                {
                    if (owner.CurrentLayer != layer)
                    {
                        owner.CurrentLayer = layer;
                    }
                }
            }
            else if (value is BaseContainerViewModel container && _documents != null)
            {
                var document = _documents.FirstOrDefault(d => d.Pages.Contains(container));
                if (document != null)
                {
                    if (CurrentDocument != document)
                    {
                        CurrentDocument = document;
                    }

                    if (CurrentContainer != container)
                    {
                        CurrentContainer = container;
                        CurrentContainer.InvalidateLayer();
                    }
                }
            }
            else if (value is DocumentContainerViewModel document)
            {
                if (CurrentDocument != document)
                {
                    CurrentDocument = document;
                    if (!CurrentDocument?.Pages.Contains(CurrentContainer) ?? false)
                    {
                        var current = CurrentDocument.Pages.FirstOrDefault();
                        if (CurrentContainer != current)
                        {
                            CurrentContainer = current;
                        }
                    }
                }
            }
        }

        public override bool IsDirty()
        {
            var isDirty = base.IsDirty();

            isDirty |= Options.IsDirty();

            foreach (var styleLibrary in StyleLibraries)
            {
                isDirty |= styleLibrary.IsDirty();
            }

            foreach (var groupLibrary in GroupLibraries)
            {
                isDirty |= groupLibrary.IsDirty();
            }

            foreach (var database in Databases)
            {
                isDirty |= database.IsDirty();
            }

            foreach (var template in Templates)
            {
                isDirty |= template.IsDirty();
            }

            foreach (var script in Scripts)
            {
                isDirty |= script.IsDirty();
            }

            foreach (var document in Documents)
            {
                isDirty |= document.IsDirty();
            }

            return isDirty;
        }

        public override void Invalidate()
        {
            base.Invalidate();

            Options.Invalidate();

            foreach (var styleLibrary in StyleLibraries)
            {
                styleLibrary.Invalidate();
            }

            foreach (var groupLibrary in GroupLibraries)
            {
                groupLibrary.Invalidate();
            }

            foreach (var database in Databases)
            {
                database.Invalidate();
            }

            foreach (var template in Templates)
            {
                template.Invalidate();
            }

            foreach (var script in Scripts)
            {
                script.Invalidate();
            }

            foreach (var document in Documents)
            {
                document.Invalidate();
            }
        }
    }
}
