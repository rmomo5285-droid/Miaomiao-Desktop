using ReactiveUI;
using ReactiveUI.Primitives;
using ServiceLib.ViewModels;
using Xunit;

namespace ServiceLib.Tests.ViewModels;

public class StatusBarViewModelTests
{
    [Fact]
    public async Task HandleOptionalInteractionAsync_WithoutHandler_DoesNotThrow()
    {
        var interaction = new Interaction<RxVoid, RxVoid>();

        await StatusBarViewModel.HandleOptionalInteractionAsync(interaction);
    }

    [Fact]
    public async Task HandleOptionalInteractionAsync_WithHandler_InvokesHandler()
    {
        var interaction = new Interaction<RxVoid, RxVoid>();
        var invoked = false;
        using var registration = interaction.RegisterHandler(context =>
        {
            invoked = true;
            context.SetOutput(RxVoid.Default);
        });

        await StatusBarViewModel.HandleOptionalInteractionAsync(interaction);

        Assert.True(invoked);
    }
}
