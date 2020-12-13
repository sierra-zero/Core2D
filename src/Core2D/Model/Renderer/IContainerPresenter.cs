﻿using Core2D.ViewModels.Containers;

namespace Core2D.Model.Renderer
{
    public interface IContainerPresenter
    {
        void Render(object dc, IShapeRenderer renderer, BaseContainerViewModel container, double dx, double dy);
    }
}
