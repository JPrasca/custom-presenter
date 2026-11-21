using System.Text;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class BiblePayloadBuilder
{
    private string _content = """
        <?xml version="1.0" encoding="utf-8"?>
        <bible name="Reina Valera 1960" abbreviation="RVR60">
          <b n="Génesis">
            <c n="1">
              <v n="1">En el principio creó Dios los cielos y la tierra.</v>
            </c>
          </b>
          <b n="Juan">
            <c n="3">
              <v n="16">Porque de tal manera amó Dios al mundo.</v>
            </c>
          </b>
        </bible>
        """;

    public BiblePayloadBuilder AsZefaniaXml()
    {
        _content = """
            <?xml version="1.0" encoding="utf-8"?>
            <XMLBIBLE biblename="Nueva Versión Internacional">
              <INFORMATION>
                <identifier>NVI</identifier>
              </INFORMATION>
              <BIBLEBOOK bnumber="19" bname="Salmos">
                <CHAPTER cnumber="23">
                  <VERS vnumber="1">El Señor es mi pastor; nada me faltará.</VERS>
                </CHAPTER>
              </BIBLEBOOK>
            </XMLBIBLE>
            """;
        return this;
    }

    public BiblePayloadBuilder AsJson()
    {
        _content = """
            {
              "name": "La Biblia de las Américas",
              "abbreviation": "LBLA",
              "verses": [
                {
                  "bookNumber": 43,
                  "bookName": "Juan",
                  "chapter": 1,
                  "verse": 1,
                  "text": "En el principio existía el Verbo."
                }
              ]
            }
            """;
        return this;
    }

    public BiblePayloadBuilder AsXxeAttack()
    {
        _content = """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE bible [
              <!ENTITY xxe SYSTEM "file:///etc/passwd">
            ]>
            <bible>
              <b n="Génesis"><c n="1"><v n="1">&xxe;</v></c></b>
            </bible>
            """;
        return this;
    }

    public BiblePayloadBuilder AsBillionLaughsAttack()
    {
        _content = """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE lolz [
              <!ENTITY lol "lol">
              <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
            ]>
            <bible><b n="Génesis"><c n="1"><v n="1">&lol2;</v></c></b></bible>
            """;
        return this;
    }

    public MemoryStream BuildStream() => new(Encoding.UTF8.GetBytes(_content));
}
