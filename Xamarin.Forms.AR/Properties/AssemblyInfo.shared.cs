using System.Runtime.CompilerServices;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

//Linker safe

#if  __ANDROID__

using Android;
[assembly: LinkerSafe]

#elif __IOS__

using Foundation;
[assembly: LinkerSafe]

#endif

//Custom xaml schema <see href="https://docs.microsoft.com/pt-br/xamarin/xamarin-forms/xaml/custom-namespace-schemas#defining-a-custom-namespace-schema"/>
[assembly: XmlnsDefinition("http://xamarin.com/schemas/2014/forms", "Xamarin.Forms.AR")]

//Recommended prefix <see href="https://docs.microsoft.com/pt-br/xamarin/xamarin-forms/xaml/custom-prefix"/>
[assembly: XmlnsPrefix("http://xamarinformsar.com/schemas/xaml", "ar")]

//Xaml compilation <see href="https://docs.microsoft.com/pt-br/xamarin/xamarin-forms/xaml/xamlc"/>
[assembly: XamlCompilation(XamlCompilationOptions.Compile)]

[assembly: InternalsVisibleTo("Xamarin.Forms.AR.Tests")]