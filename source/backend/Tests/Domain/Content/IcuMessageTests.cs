using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Tests.Domain.Content;

public class IcuMessageTests
{
    [Theory]
    [InlineData("Czat szaleje.")]
    [InlineData("{gender, select, female {Zrobiłaś to.} male {Zrobiłeś to.} other {Zrobiłoś to.}}")]
    [InlineData("Widzowie: {gender, select, female {byłaś} male {byłeś} other {byłoś}} świetna.")]
    [InlineData("Napis '''cytat''' w tekście")]
    [InlineData("{gender, select, female {A {gender, select, female {x} male {y} other {z}}} male {B} other {C}}")]
    [InlineData("{gender, select, female {Rock'n'roll} male {a} other {b}}")]
    [InlineData("{gender, select, female {a '}' b} male {b} other {c}}")]
    [InlineData("{ gender , select , female{a} male{b} other{c} }")]
    [InlineData("'{a''b}' tekst")]
    public void Accepts_the_supported_subset(string message) => Assert.Null(IcuMessage.Validate(message));

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("{gender, select, female {a} male {b}}", "missing-branch:other")]
    [InlineData("{gender, select, male {b} other {c}}", "missing-branch:female")]
    [InlineData("{gender, select, female {a} other {c}}", "missing-branch:male")]
    [InlineData("{gender, select, female {a} male {b} other {c} x {d}}", "unknown-branch:x")]
    [InlineData("{count, plural, one {a} other {b}}", "unsupported-argument")]
    [InlineData("{mood, select, female {a} male {b} other {c}}", "select-not-gender")]
    [InlineData("Otwarte { bez końca", "unbalanced")]
    [InlineData("Zamknięte } bez otwarcia", "unbalanced")]
    [InlineData("{gender}", "unsupported-argument")]
    [InlineData("{x, number}", "unsupported-argument")]
    [InlineData("{gender, select, female {a} female {b} male {c} other {d}}", "duplicate-branch:female")]
    [InlineData("Ktoś's quote '{", "unterminated-quote")]
    public void Rejects_with_a_code(string message, string code) => Assert.Equal(code, IcuMessage.Validate(message));
}
