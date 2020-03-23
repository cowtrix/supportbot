# SupportBot

SupportBot is a [Telegram](https://telegram.org/) bot for connecting a pool of Support Providers with users needing support and anonymously routing messages between them. It's written in .NET Core 3.1 and uses the great [Telegram.Bot](https://github.com/TelegramBots/telegram.bot) library.

## Getting Started

At this point you'll have to download and compile the latest source yourself.

At the root of the build location, you will need to create a file called `SupportBot_config.json`. This file will contain the configuration values for our bot. At minimum, this configuration file will need to provide the following:

* Telegram Bot Token: This links the application to the telegram bot. You can find more information about creating bots and Bot Tokens [here](https://core.telegram.org/bots). 
* Administrator Telegram IDs: these are the Telegram users who are admins. Keep this to a trusted minimum of users. These users will be able to add new Support Providers to the pool (see: *How does someone become a Support Provider?*). You can find out what your Telegram ID is under "Settings > Accounts > Telegram" in an Android phone.

An example with two administrators can be found below:

```
﻿{
	"TelegramBotToken": "13G1355290BAF7cBQQ27CxBwv8P71Hbvb4PBmfB8amiw",
	"Administrators" : 
	[
		2358343,
		3498463
	]
}
```

## FAQ

### How Does an End-User open a ticket?

All a user has to do to create a new ticket in the system is start chatting to the bot. They will first be asked to sign the contents of `clientGDPRAgreement.txt`. Replace the contents of this file with the necessary statement.

If there are no available Support Providers waiting for a ticket, they will be placed into a queue. When their position in the queue changes, they will be updated of their position with a message.

When a Support Provider is available, tickets will be assigned in a "first-come, first-served" (FIFO) order. The user will receive a message saying they've been connected, and then any messages they send to the bot will be sent to the Support Provider, and vice-versa.

If the user wishes to close their ticket at any time, all they need to do is use the command `/endchat`.

When a user wants to start a new support ticket, all they have to do is start chatting to the bot again.

### How does someone become a Support Provider?

An administrator can add a new Support Provider to the pool by sending the bot the hidden `/addnew` command. This command will generate a one-time code. You should share this code only with the person you want to be a Support Provider. They should then send that code to the bot. Doing so will promote them to Support Provider.

When a new Support User is registered, they will be asked to respond "I Accept" to the contents of `supporterGDPRAgreement.txt`. This file needs to be in the build location. Edit this file with the necessary statement.

### How does a Support Provider use the bot?

When a Support Provider is registered, the bot will ask them if they are ready to receive tickets. When you are ready to take an open ticket, press the button shown below the message. If there are any open tickets available, you will be immediately assigned to that ticket and connected with the user. 

If there are no open tickets available, you will be placed in a waiting state. When one or more tickets arrive, you'll receieve a message from the bot asking if you want to claim any of them. Press the button below that message to claim the ticket, and you will be connected to the user. Every Support Provider receives the same notifications, so you may find sometimes that someone else has claimed the one ticket available before you. In this case you are just put back to waiting again - better luck next time!

You can only have one active ticket at a time. When you are connected to a user, their messages will be rerouted through the bot and vice-versa for your messages. **The first name** of your Telegram profile will be used in an introductory message to the user when youa re connected.

If you wish to close your current ticket at any time, all you need to do is use the command `/endchat`. You will then be put back into the waiting state where you can either say you do not want to take any more tickets, or can claim another ticket if there are any.

### What data is stored?

All data is stored in the data JSON file. By default this is called `data.json` and is created automatically in the build location. You can pass a custom data JSON file path through a command argument to the executable.

Full message logs for every ticket are stored indefinitely. Tickets are not deleted upon completion. Users are identified by their Telegram ID. Messages are unobfuscated and may contain sensitive information.

A Support Provider's first name - as it appears in their Telegram profile - and Telegram ID will be stored.

In the near future there should be a way to delete a user's data to comply with any GDPR requests. This feature may even be implemented automatically via a command.