using Piranha.AttributeBuilder;
using Piranha.Models;

namespace Cms.Models;

[PageType(Title = "Standard Page", UseBlocks = true)]
public sealed class StandardPage : Page<StandardPage>
{
}
