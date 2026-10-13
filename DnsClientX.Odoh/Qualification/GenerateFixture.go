package odoh

import (
 "bytes"
 "encoding/hex"
 "encoding/json"
 "os"
 "testing"
 "github.com/cisco/go-hpke"
)

func TestDnsClientXFixture(t *testing.T) {
 suite, err := hpke.AssembleCipherSuite(ODOH_DEFAULT_KEMID, ODOH_DEFAULT_KDFID, ODOH_DEFAULT_AEADID)
 if err != nil { t.Fatal(err) }
 recipientSeed := bytes.Repeat([]byte{0x42}, 32)
 ephemeralSeed := bytes.Repeat([]byte{0x17}, 32)
 kp, err := CreateDefaultKeyPairFromSeed(recipientSeed)
 if err != nil { t.Fatal(err) }
 skE, _, err := suite.KEM.DeriveKeyPair(ephemeralSeed)
 if err != nil { t.Fatal(err) }
 skR, _, err := suite.KEM.DeriveKeyPair(recipientSeed)
 if err != nil { t.Fatal(err) }
 pkR, err := suite.KEM.DeserializePublicKey(kp.Config.Contents.PublicKeyBytes)
 if err != nil { t.Fatal(err) }
 enc, sender, err := hpke.SetupBaseS(suite, bytes.NewReader(ephemeralSeed), pkR, []byte(ODOH_LABEL_QUERY))
 if err != nil { t.Fatal(err) }
 query, _ := hex.DecodeString("123401000001000000000000076578616d706c6503636f6d0000010001")
 answer, _ := hex.DecodeString("123481800001000100000000076578616d706c6503636f6d0000010001c00c000100010000003c00047f000001")
 queryBody := CreateObliviousDNSQuery(query, 11)
 keyID := kp.Config.Contents.KeyID()
 aad := append([]byte{byte(QueryType)}, encodeLengthPrefixedSlice(keyID)...)
 ct := sender.Seal(aad, queryBody.Marshal())
 message := ObliviousDNSMessage{MessageType: QueryType, KeyID: keyID, EncryptedMessage: append(enc, ct...)}
 _, responseContext, err := kp.DecryptQuery(message)
 if err != nil { t.Fatal(err) }
 responseBody := CreateObliviousDNSResponse(answer, 7)
 response, err := responseContext.encryptResponseWithNonce(responseBody, bytes.Repeat([]byte{0x73}, 16))
 if err != nil { t.Fatal(err) }
 responseBody.Padding[0] = 1
 badPadding, err := responseContext.encryptResponseWithNonce(responseBody, bytes.Repeat([]byte{0x74}, 16))
 if err != nil { t.Fatal(err) }
 configs := CreateObliviousDoHConfigs([]ObliviousDoHConfig{kp.Config}).Marshal()
 values := map[string]string{
 "config": hex.EncodeToString(configs), "target_private_key": hex.EncodeToString(suite.KEM.SerializePrivateKey(skR)), "ephemeral_private_key": hex.EncodeToString(suite.KEM.SerializePrivateKey(skE)),
 "key_id": hex.EncodeToString(keyID), "query": hex.EncodeToString(query), "query_plaintext": hex.EncodeToString(queryBody.Marshal()),
 "encrypted_query": hex.EncodeToString(message.Marshal()), "response": hex.EncodeToString(answer), "encrypted_response": hex.EncodeToString(response.Marshal()), "bad_padding_response": hex.EncodeToString(badPadding.Marshal()),
 }
 data, _ := json.MarshalIndent(values, "", "  ")
 if err := os.WriteFile("../odoh-fixture.json", data, 0600); err != nil { t.Fatal(err) }
}
