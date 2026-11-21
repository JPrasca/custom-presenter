using System.Text;

namespace CustomPresenter.IntegrationTests.Builders;

internal sealed class BiblePayloadBuilder
{
    private string _content = """
        <?xml version="1.0" encoding="utf-8"?>
        <bible name="Reina Valera 1960" abbreviation="RVR60">
          <b n="Salmos">
            <c n="23">
              <v n="1">Jehová es mi pastor; nada me faltará.</v>
              <v n="2">En lugares de delicados pastos me hará descansar.</v>
            </c>
          </b>
        </bible>
        """;

    public BiblePayloadBuilder AsJson()
    {
        _content = """
            {
              "name": "NVI Prueba",
              "abbreviation": "NVI",
              "verses": [
                {
                  "bookNumber": 1,
                  "bookName": "Génesis",
                  "chapter": 1,
                  "verse": 1,
                  "text": "Dios, en el principio, creó los cielos y la tierra."
                }
              ]
            }
            """;
        return this;
    }

    public MemoryStream BuildStream() => new(Encoding.UTF8.GetBytes(_content));
}
