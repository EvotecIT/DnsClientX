using System.Security.Cryptography;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsMessageSigningTests {
        private const string RsaXml = "<RSAKeyValue><Modulus>53YFIke7QJ+BflZmkvTPuUtScT97JPn4N0Lvek6ukY3IOPYkOndVFXLvJSUYcI6b0RvLjaIx1wcGPcJW7+V0nUCiqnxi85roJp1NVyIPMWs/JIS5WYcv9bPbqBsAJuxGMM2m49CjlDqVlggw49Rh431e2sDeZ729AqOOlm6qV4k=</Modulus><Exponent>AQAB</Exponent><P>/DCmdjyqUn8axK4tQqzq60McH/UoDTa+1LCxfmn9aewPIkMhRidQaxwp0MdadvTDkEbKX6J9ucwO/PIHtSp4nQ==</P><Q>6vUzLeLIuypxL+vIKZr+je+7tbFlDyGFEko24F18zE1RE8kxOQ4k9BdstQchm3O/edknXEuAyvL3+kSeUf+Y3Q==</Q><DP>Ra83wAIhWixO/DvYu8zGGP3xPo9iYsxWzLSKRxEIegVFZUVBY34nhYFBuLPtNmOJyksVTnm63eUZ2yERqiizLQ==</DP><DQ>C4ecy1OlpgmfJErdt6zzcOOiwnfCDcwHS654ounzhdMFd4MX90TKa2/61adT7tzvOHt/gvfxigQCRzW2zy9LwQ==</DQ><InverseQ>rFiNryKWal8UiXOsuW46IgUGOHQGZ9IoRof5jvk428nGfrYBdKfVS5l1hU5i3Y8FTsC4NyRo3njx806i+0ZZiw==</InverseQ><D>VDqdiakC2nRxIjF86FOQWASx/qY0QPN6QVnpXd/OJQesahYgfuo4GzMVFbZXG3a5+zGbNHJmorJashTLoEcm1PWOt01L2zcLJiroBFGrMGJD2ZAITj0/OuOskfmDwk4Jgcr5o5I3eaEqG1ouR0XOYyvtI8z5U8E6o+BGrCRuCJE=</D></RSAKeyValue>";

        [Fact]
        public void SerializeDnsWireFormat_ShouldIncludeSignature() {
            using RSA rsa = RSA.Create();
            rsa.FromXmlString(RsaXml);
            var message = new DnsMessage("example.com", DnsRecordType.A, false, false, 4096, null, false, rsa);
            byte[] data = message.SerializeDnsWireFormat();

            // calculate offsets
            int offset = 12;
            foreach (var label in "example.com".Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2;
            Assert.True(data.Length > offset);

            Assert.Equal(1, (data[10] << 8) | data[11]);
            Assert.Equal(0, data[offset]);
            ushort type = (ushort)((data[offset + 1] << 8) | data[offset + 2]);
            Assert.Equal((ushort)DnsRecordType.SIG, type);
            ushort rdlen = (ushort)((data[offset + 9] << 8) | data[offset + 10]);
            byte[] signature = new byte[rdlen];
            System.Buffer.BlockCopy(data, offset + 11, signature, 0, rdlen);

            byte[] signedPortion = new byte[offset];
            System.Buffer.BlockCopy(data, 0, signedPortion, 0, offset);
            byte[] expected = rsa.SignData(signedPortion, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Assert.Equal(expected, signature);
        }
    }
}
