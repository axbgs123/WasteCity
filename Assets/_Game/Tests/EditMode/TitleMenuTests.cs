using NUnit.Framework;
using WasteCity.UI;

namespace WasteCity.Tests
{
    public sealed class TitleMenuTests
    {
        [Test] public void ContinueRequiresExistingSave(){var m=new TitleMenuModel();Assert.That(m.Continue(false),Is.False);Assert.That(m.State,Is.EqualTo(TitleMenuState.Main));Assert.That(m.Continue(true),Is.True);Assert.That(m.State,Is.EqualTo(TitleMenuState.Started));}
        [Test] public void HelpCanReturnToMainMenu(){var m=new TitleMenuModel();Assert.That(m.OpenHelp(),Is.True);Assert.That(m.State,Is.EqualTo(TitleMenuState.Help));Assert.That(m.Back(),Is.True);Assert.That(m.State,Is.EqualTo(TitleMenuState.Main));}
    }
}
