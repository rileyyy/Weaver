using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Weaver.Api.Attachments;
using Weaver.Api.Controllers;

namespace Weaver.Api.Tests;

[TestFixture]
public class DisableFormValueModelBindingAttributeTests
{
    [Test]
    public void OnResourceExecuting_RemovesOnlyTheProvidersThatReadTheForm()
    {
        var routeValues = new RouteValueProviderFactory();
        var query = new QueryStringValueProviderFactory();
        var context = new ResourceExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            [],
            new List<IValueProviderFactory>
            {
                new FormValueProviderFactory(),
                routeValues,
                new FormFileValueProviderFactory(),
                query,
                new JQueryFormValueProviderFactory(),
            });

        new DisableFormValueModelBindingAttribute().OnResourceExecuting(context);

        Assert.That(context.ValueProviderFactories, Is.EqualTo(new IValueProviderFactory[] { routeValues, query }));
    }

    // Without it MVC reads the multipart body before the action does, and every upload fails.
    [Test]
    public void Upload_DisablesFormValueModelBinding()
    {
        var upload = typeof(WorkItemAttachmentsController).GetMethod(nameof(WorkItemAttachmentsController.Upload))!;

        Assert.That(upload.GetCustomAttribute<DisableFormValueModelBindingAttribute>(), Is.Not.Null);
    }
}
