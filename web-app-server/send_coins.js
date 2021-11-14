var bitcoin = require('bitcoinjs-lib')
var bip32 = require('bip32')

var args = process.argv.slice(2);

var destAddr = args[0];
var amtToSend = parseInt(args[1]);
var strUTXO = args[2];
var xprv = args[3];

var addrPath = "m/0'/0'/0'";

var network = bitcoin.networks.bitcoin;

var fee = 10000;

var root = bip32.fromBase58(xprv, network);
var child = root.derivePath(addrPath);
const ecpair = bitcoin.ECPair.fromPublicKey(child.publicKey, { network: network });
const script = bitcoin.payments.p2wpkh({ pubkey: ecpair.publicKey, network: network });
var fromAddr = script.address;

var psbt = new bitcoin.Psbt();

var utxolines = strUTXO.split("~");
var utxoSet = [];
for (var i = 0; i < utxolines.length; i++) {
	var o = Object.create({});
	if (utxolines[i].length > 0) {
		var utxo = utxolines[i].split(",");
		o.txID = utxo[0];
		o.vout = parseInt(utxo[1]);
		o.amount = parseInt(utxo[2]);
		utxoSet.push(o);
	}
}

psbt.addOutput({ address: destAddr, value: amtToSend });

var totalAmt = 0;
for (var i = 0; i < utxoSet.length; i++)
	totalAmt += utxoSet[i].amount;

var changeAmt = totalAmt - amtToSend - fee;

if (changeAmt > 0)
	psbt.addOutput({ address: fromAddr, value: changeAmt });


for (var i = 0; i < utxoSet.length; i++) {
	psbt.addInput({
		hash: utxoSet[i].txID,
		index: utxoSet[i].vout,
		witnessUtxo: {
			script: script.output,
			value: utxoSet[i].amount,
		}
	});
}

for (var i = 0; i < utxoSet.length; i++) {
	psbt.signInput(i, child);
	psbt.validateSignaturesOfInput(i, child.publicKey);
}


psbt.finalizeAllInputs();
const tx = psbt.extractTransaction();
var strHexTransaction = tx.toHex();
console.log(strHexTransaction);