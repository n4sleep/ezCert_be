---
layout: ModuleUnit
title: Describe the shared responsibility model - Training | Microsoft Learn
canonicalUrl: https://learn.microsoft.com/en-us/training/modules/describe-cloud-compute/4-describe-shared-responsibility-model
uid: learn.wwl.describe-cloud-compute.describe-shared-responsibility-model
page_type: learn
page_kind: unit
azure_sandbox: false
sandbox: false
breadcrumb_path: /learn/breadcrumb/toc.json
feedback_system: Standard
clicktale: true
uhfHeaderId: MSDocsHeader-Learn
adobe-target: true
prefetch-feature-rollout: true
localization_scopes:
- ja-jp
- ko-kr
- zh-cn
- zh-tw
description: Describe the shared responsibility model
ms.date: 2024-12-12T00:00:00.0000000Z
author: wwlpublish
ms.author: robbarefoot
ms.topic: unit
ms.custom:
- N/A
locale: en-us
document_id: 4b21620f-23ed-3dd2-0c73-eab191695faa
document_version_independent_id: 57a8df1a-4dc3-dc2d-42ca-914f87b45135
updated_at: 2025-03-28T23:01:00.0000000Z
original_content_git_url: https://github.com/MicrosoftDocs/learn-pr/blob/live/learn-pr/wwl-azure/describe-cloud-compute/4-describe-shared-responsibility-model.yml
gitcommit: https://github.com/MicrosoftDocs/learn-pr/blob/2b643d7ee1c7d37ae4d56d4bd5b7971fad6ee3ee/learn-pr/wwl-azure/describe-cloud-compute/4-describe-shared-responsibility-model.yml
git_commit_id: 2b643d7ee1c7d37ae4d56d4bd5b7971fad6ee3ee
site_name: Docs
depot_name: Docs.learn-pr
unit_completion_type: view
feedback_product_url: ''
feedback_help_link_type: ''
feedback_help_link_url: ''
ROBOTS: noindex
asset_id: modules/describe-cloud-compute/4-describe-shared-responsibility-model
moniker_range_name: 
monikers: []
item_type: Content
source_path: learn-pr/wwl-azure/describe-cloud-compute/4-describe-shared-responsibility-model.yml
platformId: ac3d979d-31f5-6ee8-a6a4-793dee9bd30d
---

# Describe the shared responsibility model

Completed

- 3 minutes

You may have heard of the shared responsibility model, but you may not understand what it means or how it impacts cloud computing.

## How responsibilities shift in cloud

Start with a traditional on-premises datacenter. Your team is responsible for maintaining the physical space, ensuring security, and maintaining or replacing the servers if anything happens. The IT department is responsible for maintaining all the infrastructure and software needed to keep the datacenter up and running. They're also likely to be responsible for keeping all systems patched and on the correct version.

With the shared responsibility model, these responsibilities get shared between the cloud provider and the consumer. Physical security, power, cooling, and network connectivity are the responsibility of the cloud provider. The consumer isn’t collocated with the datacenter, so it wouldn’t make sense for the consumer to have any of those responsibilities.

At the same time, the consumer is responsible for the data and information stored in the cloud. (You wouldn’t want the cloud provider to be able to read your information.) The consumer is also responsible for access security, meaning you only give access to those who need it.

Then, for some things, the responsibility depends on the situation. If you’re using a cloud SQL database, the cloud provider would be responsible for maintaining the actual database. However, you’re still responsible for the data that gets ingested into the database. If you deployed a virtual machine and installed an SQL database on it, you’d be responsible for database patches and updates, as well as maintaining the data and information stored in the database.

With an on-premises datacenter, you’re responsible for everything. With cloud computing, those responsibilities shift. The shared responsibility model is heavily tied into the cloud service types (covered later in this learning path): infrastructure as a service (IaaS), platform as a service (PaaS), and software as a service (SaaS). IaaS places the most responsibility on the consumer, with the cloud provider being responsible for the basics of physical security, power, and connectivity. On the other end of the spectrum, SaaS places most of the responsibility with the cloud provider. PaaS, being a middle ground between IaaS and SaaS, rests somewhere in the middle and evenly distributes responsibility between the cloud provider and the consumer.

## Responsibility by service model

The following diagram highlights how the Shared Responsibility Model informs who is responsible for what, depending on the cloud service type.

![Diagram showing how responsibility shifts from customer to cloud provider across On-Premises, IaaS, PaaS, and SaaS service models.](../../wwl-azure/describe-cloud-compute/media/shared-responsibility-model.png)

When using a cloud provider, you’ll always be responsible for:

## What always stays with you

- The information and data stored in the cloud
- Devices that are allowed to connect to your cloud (cell phones, computers, and so on)
- The accounts and identities of the people, services, and devices within your environment

The cloud provider is always responsible for:

## What the provider always owns

- The physical datacenter
- The physical network
- The physical hosts

Your service model will determine responsibility for things like:

## What depends on the service type

- Operating systems
- Network controls
- Applications
- Identity and access
- Infrastructure

For example, identity and access is shared in PaaS and SaaS—you manage your own users, roles, and policies, while the provider runs the authentication platform (such as Microsoft Entra ID). Infrastructure, on the other hand, shifts entirely to the provider as soon as you move off-premises to IaaS.