// See https://aka.ms/new-console-template for more information

/* To Sync
Player
    Pseudo
    Type de pions

Game
    Plateau 
    Victory/Defeat
 

 Server 
    Create a game -> TTL 
 */

using gameServer.ServerHandling;

Console.WriteLine("Lancement du serveur ...");
var server = new TCPServer();
server.Start();

