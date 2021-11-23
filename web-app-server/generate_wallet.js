var bitcoin = require('bitcoinjs-lib')
var bip39 = require('bip39')
var bip32 = require('bip32')

var network = bitcoin.networks.bitcoin;

const mnemonic = bip39.generateMnemonic();
var masterSeed = bip39.mnemonicToSeedSync(mnemonic);
var node = bip32.fromSeed(masterSeed);
var xprv = node.toBase58();

var seedWords = mnemonic.split(" ");

var root = bip32.fromBase58(xprv, network);

var child = root.derivePath("m/0'/0'/0'");
var script = bitcoin.payments.p2wpkh({ pubkey: child.publicKey, network });
var addr = script.address;

console.log(mnemonic + " ," + xprv + "," + addr + ",");
