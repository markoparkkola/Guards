// See https://aka.ms/new-console-template for more information

using Guards;

var foo = new Foo();
var s = foo.Call(x => x.Success(10));
Console.WriteLine(s);
foo.Call(x => x.Success2(10));

try
{
    var s2 = foo.Call(x => x.Unsuccess(10));
    Console.WriteLine(s2);
}
catch (GuardsException ex)
{ 
    Console.WriteLine($"error: {ex.Message}");
}

try
{
    foo.Call(x => x.Unsuccess2(10));
}
catch (GuardsException ex)
{
    Console.WriteLine($"error: {ex.Message}");
}

Console.ReadKey();

class Foo 
{ 
    [Guards("i > 5")]
    public string Success(int i) => i.ToString();
    [Guards("i > 5")]
    public void Success2(int i) => Console.WriteLine($"success {i}");
    [Guards("i < 5")]
    public string Unsuccess(int i) => i.ToString();
    [Guards("i < 5")]
    public void Unsuccess2(int i) => Console.WriteLine($"success {i}");
}