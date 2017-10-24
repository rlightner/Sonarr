﻿using FluentValidation.Validators;

namespace NzbDrone.Core.Tv
{
    public class SeriesTitleSlugValidator : PropertyValidator
    {
        private readonly ISeriesService _seriesService;

        public SeriesTitleSlugValidator(ISeriesService seriesService)
            : base("Title slug is in use by another series with a similar name")
        {
            _seriesService = seriesService;
        }

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue == null) return true;

            dynamic instance = context.ParentContext.InstanceToValidate;
            var instanceId = (int)instance.Id;

            return !_seriesService.GetAllSeries().Exists(s => s.TitleSlug.Equals(context.PropertyValue.ToString()) && s.Id != instanceId);
        }
    }
}
